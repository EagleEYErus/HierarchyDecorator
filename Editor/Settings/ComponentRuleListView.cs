using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// The "Displayed Components" list: every component type in the project with a three-state display rule.
    ///
    /// A rule is only stored when it differs from the default, so the settings file stays small and a project
    /// that never touches this list writes nothing at all.
    /// </summary>
    internal sealed class ComponentRuleListView : VisualElement
    {
        private readonly HierarchyDecoratorSettings m_Settings;
        private readonly Action m_OnChanged;

        private readonly List<Type> m_Filtered = new List<Type>(256);
        private readonly Dictionary<Type, ComponentRule> m_RulesByType = new Dictionary<Type, ComponentRule>();

        private readonly ListView m_List;
        private readonly Label m_Summary;

        private string m_Search = string.Empty;
        private bool m_OverridesOnly;

        public ComponentRuleListView(HierarchyDecoratorSettings settings, Action onChanged)
        {
            m_Settings = settings;
            m_OnChanged = onChanged;

            AddToClassList("hd-component-rules");

            VisualElement toolbar = new VisualElement();
            toolbar.AddToClassList("hd-row");

            ToolbarSearchField search = new ToolbarSearchField();
            search.AddToClassList("hd-grow");
            search.RegisterValueChangedCallback(evt =>
            {
                m_Search = evt.newValue ?? string.Empty;
                Refresh();
            });

            Toggle overridesOnly = new Toggle("Overrides only")
            {
                tooltip = "Show only components that have an explicit Show or Hide rule."
            };

            overridesOnly.RegisterValueChangedCallback(evt =>
            {
                m_OverridesOnly = evt.newValue;
                Refresh();
            });

            Button clear = new Button(ClearAll) { text = "Clear All" };
            clear.tooltip = "Remove every component rule and fall back to the display mode above.";

            toolbar.Add(search);
            toolbar.Add(overridesOnly);
            toolbar.Add(clear);

            m_List = new ListView
            {
                fixedItemHeight = 20f,
                selectionType = SelectionType.None,
                showBorder = true,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = MakeItem,
                bindItem = BindItem,
                itemsSource = m_Filtered
            };

            m_List.AddToClassList("hd-component-rule-list");
            m_List.style.height = 240f;

            m_Summary = new Label();
            m_Summary.AddToClassList("hd-hint");

            Add(toolbar);
            Add(m_List);
            Add(m_Summary);

            Refresh();
        }

        public void Refresh()
        {
            RebuildRuleIndex();

            m_Filtered.Clear();

            IReadOnlyList<Type> types = ComponentCatalog.AllComponentTypes;

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];

                if (m_OverridesOnly && !m_RulesByType.ContainsKey(type))
                {
                    continue;
                }

                if (m_Search.Length > 0 && type.Name.IndexOf(m_Search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                m_Filtered.Add(type);
            }

            m_List.itemsSource = m_Filtered;
            m_List.Rebuild();

            m_Summary.text = $"{m_Filtered.Count} of {types.Count} component types · {m_RulesByType.Count} rule(s) stored";
        }

        private void RebuildRuleIndex()
        {
            m_RulesByType.Clear();

            List<ComponentRule> rules = m_Settings.ComponentRules;

            for (int i = 0; i < rules.Count; i++)
            {
                Type type = ComponentCatalog.Resolve(rules[i]);

                if (type != null)
                {
                    m_RulesByType[type] = rules[i];
                }
            }
        }

        private static VisualElement MakeItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("hd-rule-row");

            Label name = new Label { name = "type-name" };
            name.AddToClassList("hd-rule-name");

            Label assembly = new Label { name = "assembly-name" };
            assembly.AddToClassList("hd-rule-assembly");

            EnumField display = new EnumField(ComponentDisplay.Default) { name = "display" };
            display.AddToClassList("hd-rule-display");

            row.Add(name);
            row.Add(assembly);
            row.Add(display);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_Filtered.Count)
            {
                return;
            }

            Type type = m_Filtered[index];

            Label name = element.Q<Label>("type-name");
            Label assembly = element.Q<Label>("assembly-name");
            EnumField display = element.Q<EnumField>("display");

            name.text = type.Name;
            name.tooltip = type.FullName;
            assembly.text = ComponentCatalog.AssemblyNameOf(type);

            ComponentDisplay current = m_RulesByType.TryGetValue(type, out ComponentRule rule)
                ? rule.display
                : ComponentDisplay.Default;

            // The callback is re-registered per bind because rows are recycled across different types.
            display.userData = type;
            display.SetValueWithoutNotify(current);
            display.UnregisterCallback<ChangeEvent<Enum>>(OnDisplayChanged);
            display.RegisterCallback<ChangeEvent<Enum>>(OnDisplayChanged);
        }

        private void OnDisplayChanged(ChangeEvent<Enum> evt)
        {
            if (evt.currentTarget is not EnumField field || field.userData is not Type type)
            {
                return;
            }

            SetDisplay(type, (ComponentDisplay)evt.newValue);
        }

        private void SetDisplay(Type type, ComponentDisplay display)
        {
            // The index caches ComponentRule object references, and an import or a reset replaces the whole
            // list. Rebuilding first means the view can never edit an object that is no longer in it.
            RebuildRuleIndex();

            List<ComponentRule> rules = m_Settings.ComponentRules;

            Undo.RegisterCompleteObjectUndo(m_Settings, "Change Component Rule");

            if (m_RulesByType.TryGetValue(type, out ComponentRule existing))
            {
                if (display == ComponentDisplay.Default)
                {
                    rules.Remove(existing);
                    m_RulesByType.Remove(type);
                }
                else
                {
                    existing.display = display;
                }
            }
            else if (display != ComponentDisplay.Default)
            {
                ComponentRule rule = ComponentCatalog.CreateRule(type, display);

                if (rule != null)
                {
                    rules.Add(rule);
                    m_RulesByType[type] = rule;
                }
            }

            m_Summary.text = $"{m_Filtered.Count} of {ComponentCatalog.AllComponentTypes.Count} component types · {m_RulesByType.Count} rule(s) stored";
            m_OnChanged?.Invoke();
        }

        private void ClearAll()
        {
            if (m_Settings.ComponentRules.Count == 0)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(m_Settings, "Clear Component Rules");
            m_Settings.ComponentRules.Clear();
            Refresh();
            m_OnChanged?.Invoke();
        }
    }
}
