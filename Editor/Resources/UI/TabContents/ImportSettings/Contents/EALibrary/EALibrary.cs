using EAUploader.UI.Components;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.ImportSettings
{
    [System.Serializable]
    public class Article
    {
        public string title { get; set; }
        public string date { get; set; }
        public string[] tags { get; set; }
        public string[] keywords { get; set; }
        public string contentFile { get; set; }
        public string thumbnail { get; set; }
        public string id { get; set; }
    }

    public class ArticleIndex
    {
        public string id { get; set; }
        public string Title { get; set; }
        public string[] Keywords { get; set; }
        public string[] Tags { get; set; }
    }

    internal class EALibrary
    {
        private static VisualElement root;
        const string ARTICLES_FOLDER_PATH = "Packages/tech.uslog.eauploader/Editor/Resources/UI/TabContents/ImportSettings/Contents/EALibrary/Articles/EAUploader/";
        private static string currentLanguage;
        private static string currentQuerySearch = string.Empty;
        private static string currentFilterTag = string.Empty;
        private static List<ArticleIndex> articleIndexCache;

        public static void ShowContent(VisualElement rootElement)
        {
            root = rootElement;
            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/ImportSettings/Contents/EALibrary/EALibrary");
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/TabContents/ImportSettings/Contents/EALibrary/EALibrary"));
            visualTree.CloneTree(root);

            Initialize();
        }

        private static void Initialize()
        {
            currentQuerySearch = string.Empty;
            currentFilterTag = string.Empty;

            var articleContainer = root.Q<VisualElement>("article_container");
            var articleList = articleContainer.Q<ListView>("article_list");
            articleList.makeItem = MakeItem;
            articleList.bindItem = BindItem;
            articleList.itemsSource = GetArticleIndex();
            articleList.fixedItemHeight = 128;
            articleList.selectionType = SelectionType.Single;
            articleList.selectionChanged += OnSelectionChanged;

            var searchButton = root.Q<ShadowButton>("search_button");

            searchButton.clicked += () =>
            {
                var searchQuery = root.Q<TextField>("search_query");
                currentQuerySearch = searchQuery.value;
                var articleList = root.Q<ListView>("article_list");
                articleList.itemsSource = GetFilteredArticleIndex();
                articleList.Rebuild();
            };

            var filterTag = root.Q<DropdownFieldPro>("filter_tag");

            filterTag.choices = new List<string> { T7e.Get("All") };
            filterTag.choices.AddRange(GetArticleIndex().SelectMany(article => article.Tags).Distinct());
            filterTag.index = 0;


            filterTag.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == T7e.Get("Filter by Tag")) return;
                currentFilterTag = evt.newValue;
                var articleList = root.Q<ListView>("article_list");
                articleList.itemsSource = GetFilteredArticleIndex();
                articleList.Rebuild();
            });
        }

        private static VisualElement MakeItem()
        {
            var item = Resources.Load<VisualTreeAsset>("UI/TabContents/ImportSettings/Contents/EALibrary/ArticleItem");
            return item.CloneTree();
        }

        private static void BindItem(VisualElement element, int index)
        {
            var article = GetFilteredArticleIndex()[index];
            var thumbnail = GetArticleData(article.id).thumbnail;
            var imageElement = element.Q<Image>("image");

            Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnail);
            if (image != null)
            {
                imageElement.image = image;
                imageElement.style.backgroundColor = Color.clear;
            }
            else
            {
                Debug.LogError($"Image not found: {thumbnail}");
            }

            element.Q<Label>("title").text = article.Title;
        }

        private static void OnSelectionChanged(IEnumerable<object> selected)
        {
            var articleIndex = GetFilteredArticleIndex();
            if (articleIndex == null) return;

            var articleContent = root.Q<ScrollView>("article_content");
            articleContent.Clear();

            foreach (var article in selected)
            {
                if (article is ArticleIndex articleIndexItem)
                {
                    var articleData = GetArticleData(articleIndexItem.id);
                    if (articleData == null) continue;

                    var articleRenderer = new ArticleRenderer(articleData.contentFile);
                    articleContent.Add(articleRenderer);

                    root.Q<VisualElement>("article_content_container").EnableInClassList("hidden", false);
                    root.Q<VisualElement>("article_list").EnableInClassList("hidden", true);

                    var backButton = root.Q<ShadowButton>("back_button");
                    backButton.clicked += () =>
                    {
                        root.Q<VisualElement>("article_content_container").EnableInClassList("hidden", true);
                        root.Q<VisualElement>("article_list").EnableInClassList("hidden", false);
                        root.Q<ListView>("article_list").ClearSelection();
                    };
                }
            }
        }

        private static List<ArticleIndex> GetArticleIndex()
        {
            if (articleIndexCache != null && currentLanguage == LanguageUtility.GetCurrentLanguage())
            {
                return articleIndexCache;
            }

            currentLanguage = LanguageUtility.GetCurrentLanguage();
            string articlesFolderPath = ARTICLES_FOLDER_PATH + currentLanguage;
            var articleIndex = new List<ArticleIndex>();
            var articleFiles = Directory.GetFiles(articlesFolderPath, "*.json", SearchOption.AllDirectories);
            foreach (var file in articleFiles)
            {
                var articleData = JsonConvert.DeserializeObject<Article>(File.ReadAllText(file));
                // Get folder path of the root
                articleIndex.Add(new ArticleIndex
                {
                    id = Path.Combine(Path.GetDirectoryName(file)),
                    Title = articleData.title,
                    Keywords = articleData.keywords,
                    Tags = articleData.tags
                });
            }

            articleIndexCache = articleIndex;
            return articleIndex;
        }

        private static List<ArticleIndex> GetFilteredArticleIndex()
        {
            var filteredIndex = GetArticleIndex()
            .Where(article => article.Title.ToLower().Contains(currentQuerySearch.ToLower()))
            .Where(article => article.Keywords.Any(keyword => keyword.ToLower().Contains(currentQuerySearch.ToLower())))
            .ToList();

            if (!string.IsNullOrEmpty(currentFilterTag) && currentFilterTag != T7e.Get("All"))
            {
                filteredIndex = filteredIndex
                .Where(article => article.Tags.Contains(currentFilterTag))
                .ToList();
            }

            return filteredIndex;
        }

        private static Article GetArticleData(string id)
        {
            currentLanguage = LanguageUtility.GetCurrentLanguage();
            string articlesFolderPath = ARTICLES_FOLDER_PATH + currentLanguage;
            var articleFiles = Directory.GetFiles(articlesFolderPath, "*.json", SearchOption.AllDirectories);
            foreach (var file in articleFiles)
            {
                var articleData = JsonConvert.DeserializeObject<Article>(File.ReadAllText(file));
                var contentFile = Path.Combine(Path.GetDirectoryName(file), articleData.contentFile);

                articleData.contentFile = contentFile;
                articleData.thumbnail = Path.Combine(Path.GetDirectoryName(file), articleData.thumbnail);
                articleData.id = Path.Combine(Path.GetDirectoryName(file));

                if (articleData.id == id)
                {
                    return articleData;
                }
            }
            return null;
        }
    }

}