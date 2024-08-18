using EAUploader.UI.Components;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader
{

    public class DiscordWebhookSender : EditorWindow
    {
        private string WEBHOOK_URL = "";
        private static string lng = LanguageUtility.GetCurrentLanguage();
        private static bool sentFeedback = false;

        public static void OpenDiscordWebhookSenderWindow()
        {
            var window = GetWindow<DiscordWebhookSender>("Feedback");
            window.minSize = new Vector2(420, 200);
            sentFeedback = false;
        }

        private void OnEnable()
        {
            var root = rootVisualElement;

            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/styles"));

            var titleLabel = new Label(T7e.Get("Send Feedback"));
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 20;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(titleLabel);

            if (!sentFeedback)
            {
                var authorLabel = new Label(T7e.Get("Your name (option)"));
                root.Add(authorLabel);

                var authorTextField = new TextField();
                authorTextField.name = "authorName";
                root.Add(authorTextField);

                var emailLabel = new Label(T7e.Get("Mail address (option)"));
                root.Add(emailLabel);

                var emailTextField = new TextField();
                emailTextField.name = "emailAddress";
                root.Add(emailTextField);

                var feedbackTitleLabel = new Label(T7e.Get("Subject (option)"));
                root.Add(feedbackTitleLabel);

                var feedbackTitleTextField = new TextField();
                feedbackTitleTextField.name = "feedbacktitle";
                root.Add(feedbackTitleTextField);

                var messageContentLabel = new Label(T7e.Get("Message"));
                root.Add(messageContentLabel);

                var messageContentTextFieldPro = new TextFieldPro()
                {
                    required = true
                };
                messageContentTextFieldPro.name = "messageContent";
                messageContentTextFieldPro.multiline = true;
                messageContentTextFieldPro.style.height = 200;
                root.Add(messageContentTextFieldPro);

                var validationLabel = new Label(T7e.Get("Submissions are irrevocable. \nDo not include personal information."));
                root.Add(validationLabel);

                var sendButton = new Button(() =>
                {
                    var messageContent = messageContentTextFieldPro.GetValue();
                    if (!string.IsNullOrEmpty(messageContent))
                    {
                        var authorName = authorTextField.value;
                        var emailAddress = emailTextField.value;
                        var feedbacktitle = feedbackTitleTextField.value;
                        SendMessageToDiscord(WEBHOOK_URL, feedbacktitle, authorName, emailAddress, messageContent);
                        feedbackTitleTextField.SetValueWithoutNotify("");
                        authorTextField.SetValueWithoutNotify("");
                    }
                });
                sendButton.text = T7e.Get("Submit");
                root.Add(sendButton);
            }

            var closeButton = new Button(CloseWindow) { text = T7e.Get("Close") };
            closeButton.style.marginTop = 8;
            root.Add(closeButton);
        }

        private void SendMessageToDiscord(string url, string title, string author, string email, string content)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";

                content = content.Replace("\n", "\r");

                string json = BuildJson(title, author, email, content);

                try
                {
                    client.UploadString(url, json);

                    // 送信成功フラグを更新
                    sentFeedback = true;

                    // UIを更新するためのメソッド呼び出し
                    UpdateUIAfterSend();
                }
                catch (WebException e)
                {
                    Debug.LogError("Error sending webhook: " + e.Message);
                    sentFeedback = false;
                    if (e.Response != null)
                    {
                        using var stream = e.Response.GetResponseStream();
                        var reader = new StreamReader(stream);
                        Debug.LogError("Response: " + reader.ReadToEnd());
                    }
                }
            }
            Repaint();
        }

        private void UpdateUIAfterSend()
        {
            // 送信成功のメッセージを表示
            var root = rootVisualElement;
            root.Clear(); // 既存のUI要素をクリア

            if (sentFeedback)
            {
                // 送信成功のメッセージを表示
                var sentLabel = new Label(T7e.Get("Transmission was successful. Thank you."));
                root.Add(sentLabel);
            }
            else
            {
                // 送信に失敗した場合の処理
            }

            var closeButton = new Button(CloseWindow) { text = T7e.Get("Close") };
            closeButton.style.marginTop = 8;
            root.Add(closeButton);
        }

        private void CloseWindow()
        {
            Close();
        }

        private string BuildJson(string title, string author, string email, string content)
        {
            title = EscapeStringForJson(title);
            author = EscapeStringForJson(author);
            email = EscapeStringForJson(email);
            content = EscapeStringForJson(content);

            string emailField = string.IsNullOrEmpty(email) ? "\"Not provided\"" : $"\"{email}\"";

            return $"{{" +
                $"  \"content\": null," +
                $"  \"embeds\": [{{" +
                $"    \"title\": \"{title}\"," +
                $"    \"description\": \"{content}\"," +
                $"    \"color\": 36231," +
                $"    \"fields\": [" +
                $"      {{" +
                $"        \"name\": \"メールアドレス\"," +
                $"        \"value\": {emailField}" +
                $"      }}," +
                $"      {{" +
                $"        \"name\": \"言語設定\"," +
                $"        \"value\": \"{lng}\"" +
                $"      }}" +
                $"    ]," +
                $"    \"author\": {{" +
                $"      \"name\": \"{author}\"" +
                $"    }}" +
                $"  }}]" +
                $"}}";
        }

        private string EscapeStringForJson(string input)
        {
            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t")
                        .Replace("\b", "\\b")
                        .Replace("\f", "\\f");
        }
    }
}