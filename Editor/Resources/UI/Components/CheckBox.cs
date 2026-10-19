using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    // A clickable card with a round check indicator on the left and the label text beside it.
    // Unity's built-in Toggle draws a dark box that is hard to restyle, so this draws its own.
    public class CheckBox : BaseField<bool>
    {
        public new class UxmlFactory : UxmlFactory<CheckBox, UxmlTraits> { }

        public new class UxmlTraits : BaseFieldTraits<bool, UxmlBoolAttributeDescription> { }

        public static readonly new string ussClassName = "check-box";
        public static readonly string checkedUssClassName = "check-box--checked";
        public static readonly new string inputUssClassName = "check-box__input";
        public static readonly string inputMarkUssClassName = "check-box__input-mark";
        public static readonly string inputCheckedUssClassName = "check-box__input--checked";

        const float IndicatorSize = 20f;

        VisualElement m_Input;
        VisualElement m_Mark;

        public CheckBox() : this(null) { }

        public CheckBox(string label) : base(label, null)
        {
            AddToClassList(ussClassName);

            m_Input = this.Q(className: BaseField<bool>.inputUssClassName);
            m_Input.AddToClassList(inputUssClassName);
            // The label is inserted at index 0 when the uxml `label` attribute is applied, so the field's USS uses
            // row-reverse to keep this indicator on the left of the text.
            Add(m_Input);

            // Inline size so the indicator can't be squeezed by the base field's default styles.
            m_Input.style.width = IndicatorSize;
            m_Input.style.height = IndicatorSize;
            m_Input.style.minWidth = IndicatorSize;
            m_Input.style.maxWidth = IndicatorSize;
            m_Input.style.minHeight = IndicatorSize;
            m_Input.style.flexGrow = 0;
            m_Input.style.flexShrink = 0;

            m_Mark = new VisualElement();
            m_Mark.AddToClassList(inputMarkUssClassName);
            m_Mark.pickingMode = PickingMode.Ignore;
            m_Input.Add(m_Mark);

            RegisterCallback<ClickEvent>(evt => OnClick(evt));
            RegisterCallback<KeyDownEvent>(evt => OnKeydownEvent(evt));
            RegisterCallback<NavigationSubmitEvent>(evt => OnSubmit(evt));
        }

        static void OnClick(ClickEvent evt)
        {
            var checkBox = evt.currentTarget as CheckBox;
            checkBox.ToggleValue();

            evt.StopPropagation();
        }

        static void OnSubmit(NavigationSubmitEvent evt)
        {
            var checkBox = evt.currentTarget as CheckBox;
            checkBox.ToggleValue();

            evt.StopPropagation();
        }

        static void OnKeydownEvent(KeyDownEvent evt)
        {
            var checkBox = evt.currentTarget as CheckBox;

            // NavigationSubmitEvent already covers keydown events at runtime.
            if (checkBox.panel?.contextType == ContextType.Player)
                return;

            if (evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.Space)
            {
                checkBox.ToggleValue();
                evt.StopPropagation();
            }
        }

        void ToggleValue()
        {
            value = !value;
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);

            m_Input.EnableInClassList(inputCheckedUssClassName, newValue);
            EnableInClassList(checkedUssClassName, newValue);
        }
    }
}
