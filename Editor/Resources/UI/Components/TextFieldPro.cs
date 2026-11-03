using System.Collections.Generic;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    public class TextFieldPro : TextField
    {
        public new class UxmlFactory : UxmlFactory<TextFieldPro, UxmlTraits> { }

        // UI Toolkit requires this nested UxmlTraits convention. skipcq: CS-R1103
        public new class UxmlTraits : TextField.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription placeholder = new() { name = "placeholder" };
            private readonly UxmlBoolAttributeDescription required = new() { name = "required" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var textField = (TextFieldPro)ve;
                textField.placeholder = T7e.Get(placeholder.GetValueFromBag(bag, cc));
                textField.required = required.GetValueFromBag(bag, cc);
            }
        }

        public string placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value;
                _placeholderLabel.text = value ?? string.Empty;
                RefreshPlaceholder();
            }
        }

        private string _placeholder;
        private readonly Label _placeholderLabel;
        private readonly TextElement _textElement;
        private IVisualElementScheduledItem _placeholderLayoutUpdate;
        public bool required
        {
            get => _required;
            set => _required = value;
        }
        private bool _required;
        private bool loading;

        public bool Loading
        {
            get => loading;
            set
            {
                loading = value;
                SetEnabled(!loading);
                if (loading)
                {
                    text = "Loading...";
                }
                else
                {
                    if (text == "Loading...")
                    {
                        text = "";
                    }
                }
                RefreshPlaceholder();
                EnableInClassList(ussClassName + "__loading", loading);
            }
        }

        public TextFieldPro()
        {
            _placeholderLabel = new Label { pickingMode = PickingMode.Ignore };
            _placeholderLabel.AddToClassList("text-field-pro__placeholder");
            var input = this.Q<TextInputBase>();
            _textElement = input.Q<TextElement>();
            input.Add(_placeholderLabel);
            _textElement.RegisterCallback<GeometryChangedEvent>(_ => UpdatePlaceholderLayout());
            _placeholderLabel.RegisterCallback<GeometryChangedEvent>(_ => UpdatePlaceholderLayout());
            RegisterCallback<InputEvent>(_ => RefreshPlaceholder());
            this.RegisterValueChangedCallback(ValueChanged);
            RefreshPlaceholder();
        }

        public override void SetValueWithoutNotify(string newValue)
        {
            base.SetValueWithoutNotify(newValue);
            RefreshPlaceholder();
        }

        public void Reset()
        {
            RefreshPlaceholder();
            EnableInClassList("border-red", false);
        }

        private void ValueChanged(ChangeEvent<string> evt)
        {
            RefreshPlaceholder();
            if (!_required) return;
            this.Q<TextInputBase>().EnableInClassList("border-red", string.IsNullOrWhiteSpace(evt.newValue));
        }

        public string GetValue()
        {
            if (_required && IsPlaceholder())
            {
                this.Q<TextInputBase>().EnableInClassList("border-red", true);
                return null;
            }
            if (IsPlaceholder())
            {
                return null;
            }
            return text;
        }

        private void RefreshPlaceholder()
        {
            _placeholderLabel?.EnableInClassList("text-field-pro__placeholder--hidden", !IsPlaceholder());
            _placeholderLayoutUpdate?.Pause();
            if (IsPlaceholder() && _textElement?.panel != null)
            {
                _placeholderLayoutUpdate = schedule.Execute(() =>
                {
                    _placeholderLayoutUpdate = null;
                    UpdatePlaceholderLayout();
                });
            }
        }

        private void UpdatePlaceholderLayout()
        {
            var parent = _placeholderLabel.parent;
            var rect = _textElement.ChangeCoordinatesTo(parent, _textElement.contentRect);
            var lineOffset = 0f;
            if (multiline && IsPlaceholder())
            {
                var cursorLineHeight = _textElement.MeasureTextSize("\u200b", 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).y;
                var firstLine = _placeholderLabel.text.Split('\n')[0];
                var placeholderLineHeight = _placeholderLabel.MeasureTextSize(string.IsNullOrEmpty(firstLine) ? "\u200b" : firstLine,
                    0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).y;
                if (cursorLineHeight > 1f && placeholderLineHeight > 1f)
                    lineOffset = (cursorLineHeight - placeholderLineHeight) * 0.5f;
            }
            _placeholderLabel.style.left = rect.x - parent.resolvedStyle.borderLeftWidth;
            _placeholderLabel.style.top = rect.y - parent.resolvedStyle.borderTopWidth + lineOffset;
            _placeholderLabel.style.width = rect.width;
            _placeholderLabel.style.height = rect.height;
        }

        public bool IsPlaceholder()
        {
            return !loading && !string.IsNullOrWhiteSpace(_placeholder) && string.IsNullOrEmpty(text);
        }

        public void ClearValue()
        {
            SetValueWithoutNotify("");
        }
    }
}
