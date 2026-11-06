/**
 * VPMレジストリ (registry.json) を生成する。
 *
 * - パッケージのzipは各GitHubリリースのアセットとして保存する (一度アップロードしたzipは作り直さない)。
 * - GitHub Pagesには registry.json だけを置く。
 * - 公開中の registry.json を元に、新しいバージョンだけを追加する。
 * - リリースにzipがあるのに registry.json に載っていないバージョンも追加し直す (途中で失敗した場合の自己修復)。
 * - 旧構成 (Pages上の packages/*.zip) のバージョンは、同じzipをリリースへ移してURLを書き換える。
 *
 * 環境変数:
 *   TAG_NAME            追加するリリースタグ (例: v1.6.3)。空なら既存リリースからの再生成のみ。
 *   PACKAGE_SOURCE_PATH TAG_NAME のタグをチェックアウトしたフォルダ。
 *   GITHUB_REPOSITORY   owner/name
 *   GH_TOKEN            gh CLI 用のトークン
 *   DRY_RUN=1           リリースへアップロードしない (ローカル確認用)
 */
import fs from 'node:fs/promises';
import path from 'node:path';
import url from 'node:url';
import crypto from 'node:crypto';
import os from 'node:os';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import AdmZip from 'adm-zip';
import yaml from 'js-yaml';

const execFileAsync = promisify(execFile);

/** VPMの依存関係へ含めないパッケージ名の接頭辞。 */
const IGNORE_VPM_DEPENDENCY_PREFIX = 'com.unity.';

const vpmDirectoryPath = path.dirname(url.fileURLToPath(import.meta.url));
const siteDirectoryPath = path.join(vpmDirectoryPath, 'site');
const repository = process.env.GITHUB_REPOSITORY || 'eauploader/EAUploader-for-VRChat';
const tagName = (process.env.TAG_NAME || '').trim();
const packageSourcePath = process.env.PACKAGE_SOURCE_PATH
  ? path.resolve(process.env.PACKAGE_SOURCE_PATH)
  : null;
const dryRun = process.env.DRY_RUN === '1';
const releaseDownloadPrefix = `https://github.com/${repository}/releases/download/`;

const template = yaml.load(await fs.readFile(path.join(vpmDirectoryPath, 'registry-template.yaml'), 'utf8'));
const partialManifests = yaml.load(await fs.readFile(path.join(vpmDirectoryPath, 'partial-manifests.yaml'), 'utf8')) ?? {};

function notice(message) {
  console.log(process.env.GITHUB_ACTIONS ? `::notice::${message}` : message);
}

async function gh(...args) {
  const { stdout } = await execFileAsync('gh', args, { maxBuffer: 256 * 1024 * 1024 });
  return stdout;
}

function sha256(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

function releaseAssetURL(tag, fileName) {
  return releaseDownloadPrefix + encodeURIComponent(tag) + '/' + encodeURIComponent(fileName);
}

async function download(downloadURL) {
  const response = await fetch(downloadURL);
  if (!response.ok) {
    throw new Error(`ダウンロードに失敗しました (${response.status}): ${downloadURL}`);
  }
  return Buffer.from(await response.arrayBuffer());
}

async function uploadReleaseAsset(tag, fileName, buffer) {
  if (dryRun) {
    notice(`[DRY_RUN] ${tag} へ ${fileName} をアップロードします。`);
    return;
  }
  const temporaryDirectoryPath = await fs.mkdtemp(path.join(os.tmpdir(), 'vpm-'));
  try {
    const filePath = path.join(temporaryDirectoryPath, fileName);
    await fs.writeFile(filePath, buffer);
    await gh('release', 'upload', tag, filePath, '--repo', repository);
  } finally {
    await fs.rm(temporaryDirectoryPath, { recursive: true, force: true });
  }
}

/** 公開中の registry.json を取得する。まだ公開されていなければテンプレートを返す。 */
async function loadPublishedRegistry() {
  // Pagesのキャッシュで古い registry.json を読まないようにする
  const response = await fetch(`${template.url}?t=${Date.now()}`, { headers: { 'Cache-Control': 'no-cache' } });
  if (response.status === 404) {
    notice('公開中のレジストリが見つからないため、新規に作成します。');
    return structuredClone(template);
  }
  if (!response.ok) {
    // 取得できないまま進めると既存のバージョンが消えるため、ここで止める
    throw new Error(`公開中のレジストリを取得できませんでした (${response.status}): ${template.url}`);
  }
  const registry = await response.json();
  return { ...template, packages: registry.packages ?? {} };
}

async function fetchReleases() {
  const pages = JSON.parse(await gh('api', '--paginate', '--slurp', `repos/${repository}/releases?per_page=100`));
  return new Map(pages.flat().filter(({ draft }) => !draft).map(release => [release.tag_name, release]));
}

/** リリースに添付済みのzipの内容を確認し、レジストリ用のマニフェストを返す。 */
async function manifestFromReleaseAsset(tag, asset) {
  const buffer = await download(asset.browser_download_url);
  const manifest = JSON.parse(new AdmZip(buffer).readAsText('package.json'));
  manifest.url = releaseAssetURL(tag, asset.name);
  manifest.zipSHA256 = sha256(buffer);
  return manifest;
}

/** タグをチェックアウトしたフォルダから、UPMと同じファイル構成のzipを作る。 */
async function buildPackage(sourcePath, tag) {
  // UPM (OpenUPM) と同じく package.json の files に従ってファイルを集める
  const npmArgs = ['pack', '--dry-run', '--json', '--ignore-scripts'];
  const options = { cwd: sourcePath, maxBuffer: 64 * 1024 * 1024 };
  const { stdout } = process.env.npm_execpath
    ? await execFileAsync(process.execPath, [process.env.npm_execpath, ...npmArgs], options)
    : await execFileAsync('npm', npmArgs, options);
  const [{ files }] = JSON.parse(stdout);

  const manifest = JSON.parse(await fs.readFile(path.join(sourcePath, 'package.json'), 'utf8'));
  if (manifest.dependencies) {
    manifest.vpmDependencies = {
      ...manifest.vpmDependencies,
      ...Object.fromEntries(Object.entries(manifest.dependencies)
        .filter(([name]) => !name.startsWith(IGNORE_VPM_DEPENDENCY_PREFIX))),
    };
  }
  Object.assign(manifest, partialManifests[manifest.name]);
  const fileName = `${manifest.name}-${manifest.version}.zip`;
  manifest.url = releaseAssetURL(tag, fileName);

  const zip = new AdmZip();
  for (const { path: filePath } of files.sort((a, b) => a.path.localeCompare(b.path))) {
    if (filePath !== 'package.json') {
      zip.addFile(filePath, await fs.readFile(path.join(sourcePath, filePath)));
    }
  }
  zip.addFile('package.json', Buffer.from(JSON.stringify(manifest, null, '\t')));
  const buffer = zip.toBuffer();
  manifest.zipSHA256 = sha256(buffer);
  return { manifest, fileName, buffer };
}

const registry = await loadPublishedRegistry();
const releases = await fetchReleases();

// 1. 旧構成 (Pages上のzip) のバージョンをリリースのアセットへ移す
for (const [name, { versions }] of Object.entries(registry.packages)) {
  for (const [version, manifest] of Object.entries(versions)) {
    if (manifest.url.startsWith(releaseDownloadPrefix)) {
      continue;
    }
    const tag = 'v' + version;
    const release = releases.get(tag);
    if (!release) {
      throw new Error(`${name}@${version} の移行先のリリース ${tag} が見つかりません。`);
    }
    const fileName = path.posix.basename(new URL(manifest.url).pathname);
    const asset = release.assets.find(asset => asset.name === fileName);
    if (asset) {
      const { zipSHA256 } = await manifestFromReleaseAsset(tag, asset);
      if (zipSHA256 !== manifest.zipSHA256) {
        throw new Error(`${tag} の ${fileName} がレジストリのハッシュと一致しません。`);
      }
    } else {
      const buffer = await download(manifest.url);
      if (sha256(buffer) !== manifest.zipSHA256) {
        throw new Error(`${manifest.url} がレジストリのハッシュと一致しません。`);
      }
      notice(`${name}@${version} のzipをリリース ${tag} へ移します。`);
      await uploadReleaseAsset(tag, fileName, buffer);
    }
    manifest.url = releaseAssetURL(tag, fileName);
  }
}

// 2. リリースにzipがあるのにレジストリにないバージョンを追加する
const registeredFileNames = new Set(Object.entries(registry.packages)
  .flatMap(([name, { versions }]) => Object.keys(versions).map(version => `${name}-${version}.zip`)));
for (const [tag, release] of releases) {
  for (const asset of release.assets) {
    // 「<パッケージ名>-<タグのバージョン>.zip」だけを対象にする
    if (!asset.name.endsWith(`-${tag.replace(/^v/, '')}.zip`) || registeredFileNames.has(asset.name)) {
      continue;
    }
    const manifest = await manifestFromReleaseAsset(tag, asset);
    if (asset.name !== `${manifest.name}-${manifest.version}.zip`) {
      continue;
    }
    notice(`リリース ${tag} の ${manifest.name}@${manifest.version} をレジストリへ追加します。`);
    registry.packages[manifest.name] ??= { versions: {} };
    registry.packages[manifest.name].versions[manifest.version] = manifest;
    registeredFileNames.add(asset.name);
  }
}

// 3. 今回のタグのパッケージを作成してリリースへ添付する
if (tagName) {
  if (!releases.has(tagName)) {
    throw new Error(`リリース ${tagName} が見つかりません。`);
  }
  if (!packageSourcePath) {
    throw new Error('PACKAGE_SOURCE_PATH が指定されていません。');
  }
  const { name, version } = JSON.parse(await fs.readFile(path.join(packageSourcePath, 'package.json'), 'utf8'));
  if ('v' + version !== tagName) {
    throw new Error(`タグ ${tagName} と package.json のバージョン ${version} が一致しません。`);
  }
  if (registry.packages[name]?.versions[version]) {
    notice(`${name}@${version} はレジストリへ追加済みです。`);
  } else {
    const { manifest, fileName, buffer } = await buildPackage(packageSourcePath, tagName);
    notice(`${name}@${version} を作成し、リリース ${tagName} へ添付します。`);
    await uploadReleaseAsset(tagName, fileName, buffer);
    if (dryRun) {
      const dryRunDirectoryPath = path.join(vpmDirectoryPath, 'dry-run');
      await fs.mkdir(dryRunDirectoryPath, { recursive: true });
      await fs.writeFile(path.join(dryRunDirectoryPath, fileName), buffer);
    }
    registry.packages[name] ??= { versions: {} };
    registry.packages[name].versions[version] = manifest;
  }
}

// 4. Pages用のフォルダへ registry.json だけを書き出す
await fs.rm(siteDirectoryPath, { recursive: true, force: true });
await fs.mkdir(siteDirectoryPath, { recursive: true });
await fs.writeFile(path.join(siteDirectoryPath, 'registry.json'), JSON.stringify(registry, null, '\t'));
for (const [name, { versions }] of Object.entries(registry.packages)) {
  notice(`${name}: ${Object.keys(versions).length} バージョン (最新 ${Object.keys(versions).at(-1)})`);
}
