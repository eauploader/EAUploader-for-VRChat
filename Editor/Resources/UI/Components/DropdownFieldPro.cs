using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    // Looks and behaves like DropdownField, but the list is drawn with UI Toolkit instead of the native
    // editor menu, so it follows the rest of the UI's design.
    //
    // With multiSelect on, several choices can be ticked at once: the list stays open while you pick,
    // the field shows the ticked names (or the "All" text when everything is ticked), and changes are
    // reported through selectionChanged instead of the value-changed callback.
    // With segmented on, the choices are shown side by side as a segmented control instead of a field and a list.
    // With emptyMeansAll also on, ticking nothing counts as "All" too: the field shows the "All" text and the
    // last tick can be removed.
    public class DropdownFieldPro : BaseField<string>
    {
        public new class UxmlFactory : UxmlFactory<DropdownFieldPro, UxmlTraits> { }

        public new class UxmlTraits : BaseFieldTraits<string, UxmlStringAttributeDescription>
        {
            private readonly UxmlStringAttributeDescription m_Choices = new() { name = "choices" };
            private readonly UxmlIntAttributeDescription m_Index = new() { name = "index", defaultValue = -1 };
            private readonly UxmlBoolAttributeDescription m_MultiSelect = new() { name = "multi-select" };
            private readonly UxmlBoolAttributeDescription m_Segmented = new() { name = "segmented" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var field = (DropdownFieldPro)ve;

                field.multiSelect = m_MultiSelect.GetValueFromBag(bag, cc);
                field.segmented = m_Segmented.GetValueFromBag(bag, cc);

                var choices = m_Choices.GetValueFromBag(bag, cc);
                if (!string.IsNullOrEmpty(choices))
                {
                    field.choices = new List<string>(choices.Split(','));
                }

                var index = m_Index.GetValueFromBag(bag, cc);
                if (index >= 0)
                {
                    field.index = index;
                }
            }
        }

        public new const string ussClassName = "dropdown-pro";
        public new const string inputUssClassName = "dropdown-pro__input";
        public const string openUssClassName = "dropdown-pro--open";
        public const string textUssClassName = "dropdown-pro__text";
        public const string arrowUssClassName = "dropdown-pro__arrow";
        public const string overlayUssClassName = "dropdown-pro__overlay";
        public const string shadowUssClassName = "dropdown-pro__shadow";
        public const string menuUssClassName = "dropdown-pro__menu";
        public const string itemUssClassName = "dropdown-pro__item";
        public const string itemSelectedUssClassName = "dropdown-pro__item--selected";
        public const string itemCheckUssClassName = "dropdown-pro__item-check";
        public const string itemLabelUssClassName = "dropdown-pro__item-label";
        public const string segmentsUssClassName = "dropdown-pro__segments";
        public const string segmentIndicatorUssClassName = "dropdown-pro__segment-indicator";
        public const string segmentIndicatorBoxUssClassName = "dropdown-pro__segment-indicator-box";
        public const string segmentUssClassName = "dropdown-pro__segment";
        public const string segmentActiveUssClassName = "dropdown-pro__segment--active";

        // Gap between the field and the opened list, and the minimum distance kept from the window edges.
        private const float MenuGap = 4f;
        private const float WindowMargin = 4f;
        // The list slides this far (px) while it fades in. It comes from the side it opens away from.
        internal const float SlideDistance = 8f;
        // Rough size of the list before it has been laid out; only used to pick the slide direction. Keep in sync with the USS.
        private const float EstimatedItemHeight = 29f;
        private const float EstimatedMenuPadding = 10f;
        private const float MenuMaxHeight = 320f;

        private readonly VisualElement _input;
        private readonly Label _text;
        private List<string> _choices = new List<string>();
        private int _index = -1;
        private bool _multiSelect;
        private readonly HashSet<int> _selected = new HashSet<int>();
        private bool _emptyMeansAll;
        private string _allText = "All";
        private VisualElement _overlay;
        private bool _segmented;
        private VisualElement _segments;
        private VisualElement _segmentIndicator;
        private readonly List<VisualElement> _segmentButtons = new List<VisualElement>();

        /// <summary>Raised when the ticked choices change by user input (multiSelect only). Indices are in ascending order.</summary>
        public event Action<IReadOnlyList<int>> selectionChanged;

        public List<string> choices
        {
            get => _choices;
            set
            {
                _choices = value ?? new List<string>();
                SetValueWithoutNotify(this.value);
                RebuildSegments();
            }
        }

        public int index
        {
            get => _index;
            set
            {
                if (value >= 0 && value < _choices.Count)
                {
                    this.value = _choices[value];
                }
                else
                {
                    SetValueWithoutNotify(null);
                }
            }
        }

        public bool multiSelect
        {
            get => _multiSelect;
            set
            {
                _multiSelect = value;
                RefreshText();
            }
        }

        /// <summary>Shows the choices side by side as a segmented control (single select only).</summary>
        public bool segmented
        {
            get => _segmented;
            set
            {
                _segmented = value;
                RebuildSegments();
            }
        }

        /// <summary>When on, having nothing ticked is shown as "All" and is allowed (multiSelect only).</summary>
        public bool emptyMeansAll
        {
            get => _emptyMeansAll;
            set
            {
                _emptyMeansAll = value;
                RefreshText();
            }
        }

        /// <summary>Text shown in the field when every choice is ticked (multiSelect only).</summary>
        public string allText
        {
            get => _allText;
            set
            {
                _allText = value;
                RefreshText();
            }
        }

        // Return a sorted immutable snapshot without exposing mutable selection state. skipcq: CS-P1026
        public IReadOnlyList<int> selectedIndices => _selected.OrderBy(i => i).ToList();

        public DropdownFieldPro() : this(null, null, -1) { }

        public DropdownFieldPro(List<string> choices, int defaultIndex) : this(null, choices, defaultIndex) { }

        public DropdownFieldPro(string label, List<string> choices, int defaultIndex) : base(label, null)
        {
            AddToClassList(ussClassName);
            focusable = true;
            tabIndex = 0;

            _input = this.Q(className: BaseField<string>.inputUssClassName);
            _input.AddToClassList(inputUssClassName);

            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList(textUssClassName);
            _input.Add(_text);

            var arrow = new MaterialIcon { icon = "arrow_drop_down", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList(arrowUssClassName);
            _input.Add(arrow);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                ShowMenu();
                evt.StopPropagation();
            });
            RegisterCallback<DetachFromPanelEvent>(evt => CloseMenu(false));

            if (choices != null)
            {
                _choices = choices;
            }
            if (defaultIndex >= 0 && defaultIndex < _choices.Count)
            {
                SetValueWithoutNotify(_choices[defaultIndex]);
            }
        }

        public override void SetValueWithoutNotify(string newValue)
        {
            base.SetValueWithoutNotify(newValue);

            _index = newValue == null ? -1 : _choices.IndexOf(newValue);
            RefreshText();
            RefreshSegments();
        }

        private void RebuildSegments()
        {
            if (!_segmented)
            {
                _segments?.RemoveFromHierarchy();
                _segments = null;
                _segmentIndicator = null;
                _segmentButtons.Clear();
                _input.style.display = DisplayStyle.Flex;
                return;
            }

            _input.style.display = DisplayStyle.None;
            if (_segments == null)
            {
                _segments = new VisualElement();
                _segments.AddToClassList(segmentsUssClassName);
                _segments.RegisterCallback<GeometryChangedEvent>(_ => UpdateSegmentIndicator());
                Add(_segments);
            }

            _segments.Clear();
            _segmentButtons.Clear();

            _segmentIndicator = new Shadow
            {
                shadowBlur = 6,
                shadowOffsetY = 2,
                shadowOpacity = 0.08f,
                pickingMode = PickingMode.Ignore
            };
            _segmentIndicator.AddToClassList(segmentIndicatorUssClassName);
            var indicatorBox = new VisualElement { pickingMode = PickingMode.Ignore };
            indicatorBox.AddToClassList(segmentIndicatorBoxUssClassName);
            _segmentIndicator.Add(indicatorBox);
            _segments.Add(_segmentIndicator);
            for (var i = 0; i < _choices.Count; i++)
            {
                var choice = i;
                var segment = new Button(() => index = choice);
                segment.AddToClassList(segmentUssClassName);
                segment.Add(new Label(_choices[i]));
                segment.RegisterCallback<GeometryChangedEvent>(_ => UpdateSegmentIndicator());
                _segments.Add(segment);
                _segmentButtons.Add(segment);
            }

            RefreshSegments();
        }

        private void RefreshSegments()
        {
            if (_segments == null) return;

            for (var i = 0; i < _segmentButtons.Count; i++)
            {
                _segmentButtons[i].EnableInClassList(segmentActiveUssClassName, i == _index);
            }
            UpdateSegmentIndicator();
        }

        private void UpdateSegmentIndicator()
        {
            if (_segmentIndicator == null) return;

            if (_index < 0 || _index >= _segmentButtons.Count)
            {
                _segmentIndicator.style.display = DisplayStyle.None;
                return;
            }

            var activeBtn = _segmentButtons[_index];
            var rect = activeBtn.layout;
            if (float.IsNaN(rect.width) || rect.width <= 0f) return;

            _segmentIndicator.style.display = DisplayStyle.Flex;
            _segmentIndicator.style.width = rect.width;
            _segmentIndicator.style.height = rect.height;
            _segmentIndicator.style.translate = new Translate(rect.x, rect.y);
        }

        /// <summary>Ticks exactly these choices (multiSelect only) without raising selectionChanged.</summary>
        public void SetSelectedIndicesWithoutNotify(IEnumerable<int> indices)
        {
            _selected.Clear();
            foreach (var i in indices)
            {
                if (i >= 0 && i < _choices.Count)
                {
                    _selected.Add(i);
                }
            }
            RefreshText();
        }

        private void RefreshText()
        {
            if (!_multiSelect)
            {
                _text.text = value ?? string.Empty;
                return;
            }

            if ((_choices.Count > 0 && _selected.Count == _choices.Count)
                || (_emptyMeansAll && _selected.Count == 0))
            {
                _text.text = _allText;
                return;
            }

            _text.text = string.Join(", ", _selected.OrderBy(i => i).Select(i => _choices[i]));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !_input.worldBound.Contains(evt.position))
            {
                return;
            }

            ShowMenu();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Space)
            {
                ShowMenu();
                evt.StopPropagation();
            }
        }

        private bool IsSelected(int i)
        {
            return _multiSelect ? _selected.Contains(i) : i == _index;
        }

        private void ShowMenu()
        {
            if (_overlay != null || panel == null || _choices.Count == 0 || _segmented)
            {
                return;
            }

            // The topmost element under the panel root is the one that carries the stylesheets.
            VisualElement top = this;
            while (top.parent != null && top.parent != panel.visualTree)
            {
                top = top.parent;
            }

            var fieldRect = new Rect(top.WorldToLocal(_input.worldBound.position), _input.worldBound.size);

            // A transparent layer over everything: a click anywhere outside the list closes it and goes no further.
            var overlay = new VisualElement { focusable = true };
            overlay.AddToClassList(overlayUssClassName);
            overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                // Pointer-downs on the list bubble up to here as well. Closing for those would remove the list before the
                // click is completed, and the click would land on whatever is underneath.
                if (evt.target != overlay)
                {
                    return;
                }

                CloseMenu(true);
                evt.StopPropagation();
            });
            overlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    CloseMenu(true);
                    evt.StopPropagation();
                }
            });

            var shadow = new Shadow { shadowBlur = 16, shadowOffsetY = 4, shadowOpacity = 0.1f };
            shadow.AddToClassList(shadowUssClassName);

            var menu = new ScrollView(ScrollViewMode.Vertical);
            menu.AddToClassList(menuUssClassName);

            VisualElement firstSelectedItem = null;
            for (int i = 0; i < _choices.Count; i++)
            {
                var item = CreateItem(i);
                if (firstSelectedItem == null && IsSelected(i))
                {
                    firstSelectedItem = item;
                }
                menu.Add(item);
            }

            shadow.Add(menu);
            overlay.Add(shadow);

            // Start below the field, transparent and shifted a little towards the field. Once the list has a size,
            // place it for real (flipped above the field when it would run off the window, kept inside the window
            // sideways) and fade it in while it slides to its position. The transition itself is in the USS.
            var estimatedHeight = Mathf.Min(MenuMaxHeight, _choices.Count * EstimatedItemHeight + EstimatedMenuPadding);
            var opensUpwards = fieldRect.yMax + MenuGap + estimatedHeight > top.layout.height - WindowMargin
                && fieldRect.y - MenuGap - estimatedHeight >= WindowMargin;

            shadow.style.left = fieldRect.x;
            shadow.style.top = fieldRect.yMax + MenuGap;
            shadow.style.minWidth = fieldRect.width;
            shadow.style.opacity = 0f;
            shadow.style.translate = new Translate(0f, opensUpwards ? SlideDistance : -SlideDistance);

            var placed = false;
            shadow.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (placed || evt.newRect.width <= 0f || evt.newRect.height <= 0f)
                {
                    return;
                }
                placed = true;

                var width = evt.newRect.width;
                var height = evt.newRect.height;

                var y = fieldRect.yMax + MenuGap;
                if (y + height > top.layout.height - WindowMargin)
                {
                    var above = fieldRect.y - MenuGap - height;
                    y = above >= WindowMargin ? above : Mathf.Max(WindowMargin, top.layout.height - WindowMargin - height);
                }

                var x = Mathf.Max(WindowMargin, Mathf.Min(fieldRect.x, top.layout.width - WindowMargin - width));

                shadow.style.left = x;
                shadow.style.top = y;

                // One tick later, so the transparent state is rendered first and the change below is animated.
                shadow.schedule.Execute(() =>
                {
                    shadow.style.opacity = 1f;
                    shadow.style.translate = new Translate(0f, 0f);
                });

                if (firstSelectedItem != null)
                {
                    menu.ScrollTo(firstSelectedItem);
                }
            });

            _overlay = overlay;
            // While the list is open the pointer is over the list, so keep the field tinted as if hovered.
            EnableInClassList(openUssClassName, true);
            top.Add(overlay);
            overlay.Focus();
        }

        private VisualElement CreateItem(int i)
        {
            var item = new VisualElement();
            item.AddToClassList(itemUssClassName);

            var label = new Label(_choices[i]) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(itemLabelUssClassName);

            var check = new MaterialIcon { pickingMode = PickingMode.Ignore };
            check.AddToClassList(itemCheckUssClassName);

            item.Add(label);
            item.Add(check);
            ApplyItemState(item, check, IsSelected(i));

            item.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();

                if (!_multiSelect)
                {
                    CloseMenu(true);
                    index = i;
                    return;
                }

                // Multi-select: toggle and keep the list open. Unless emptyMeansAll is on, one choice always stays ticked.
                var nowSelected = !_selected.Contains(i);
                if (!nowSelected && _selected.Count == 1 && !_emptyMeansAll)
                {
                    return;
                }

                if (nowSelected)
                {
                    _selected.Add(i);
                }
                else
                {
                    _selected.Remove(i);
                }

                ApplyItemState(item, check, nowSelected);
                RefreshText();
                selectionChanged?.Invoke(selectedIndices);
            });
            return item;
        }

        private static void ApplyItemState(VisualElement item, MaterialIcon check, bool selected)
        {
            item.EnableInClassList(itemSelectedUssClassName, selected);
            check.icon = selected ? "check" : string.Empty;
        }

        private void CloseMenu(bool refocus)
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.RemoveFromHierarchy();
            _overlay = null;
            EnableInClassList(openUssClassName, false);

            if (refocus && panel != null)
            {
                Focus();
            }
        }
    }
}
