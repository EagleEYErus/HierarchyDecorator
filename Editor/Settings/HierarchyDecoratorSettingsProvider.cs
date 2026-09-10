using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// The Project Settings and Preferences pages, built with UI Toolkit.
    ///
    /// Everything editable is bound through a <see cref="SerializedObject"/>, which is what gives correct,
    /// granular undo on a ScriptableSingleton for free. The hierarchy render path never touches
    /// SerializedObject - it reads the plain C# fields.
    ///
    /// There is no mock preview pane: decorations apply to the real Hierarchy window as you type, which is a
    /// better preview than anything that could be faked here.
    /// </summary>
    internal static class HierarchyDecoratorSettingsProvider
    {
        private static readonly string[] Keywords =
        {
            "hierarchy", "decorator", "header", "separator", "breadcrumb", "tree line", "guide line",
            "component", "icon", "preset", "missing script", "alternating", "zebra", "row"
        };

        [SettingsProvider]
        private static SettingsProvider CreateProjectSettings()
        {
            return new SettingsProvider(PackageInfo.SettingsMenuPath, SettingsScope.Project, Keywords)
            {
                label = PackageInfo.DisplayName,
                activateHandler = (_, root) => BuildProjectSettings(root),
                deactivateHandler = () => HierarchyDecoratorSettings.instance.Persist()
            };
        }

        [SettingsProvider]
        private static SettingsProvider CreateUserPreferences()
        {
            return new SettingsProvider(PackageInfo.PreferencesMenuPath, SettingsScope.User, Keywords)
            {
                label = PackageInfo.DisplayName,
                activateHandler = (_, root) => BuildUserPreferences(root),
                deactivateHandler = () => HierarchyDecoratorUserSettings.instance.Persist()
            };
        }

        private static void BuildProjectSettings(VisualElement root)
        {
            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;
            SerializedObject serialized = new SerializedObject(settings);

            ApplyStyle(root);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("hd-settings-root");
            root.Add(scroll);

            scroll.Add(BuildLegacyWarning());
            scroll.Add(BuildPresetSection(settings));

            HelpBox headerDiagnostics = CreateDiagnosticsBox();
            HelpBox componentDiagnostics = CreateDiagnosticsBox();

            VisualElement headerSection = Section("Headers & Separators",
                "Rows whose name starts with a rule's prefix become section markers. " +
                "Rules are matched top to bottom and the first match wins.",
                new PropertyField(serialized.FindProperty("m_HeaderRules"), string.Empty));

            headerSection.Add(headerDiagnostics);
            scroll.Add(headerSection);

            scroll.Add(Section("Tree Guide Lines",
                "Drawn from the object's real depth. Automatically hidden while the Hierarchy search filter " +
                "is active, because Unity flattens the list and removes indentation there.",
                new PropertyField(serialized.FindProperty("m_TreeLines"), string.Empty)));

            scroll.Add(Section("Rows",
                "Unity 6.6 draws alternating rows natively. Only enable the override if you want different " +
                "colours than the editor's.",
                new PropertyField(serialized.FindProperty("m_Rows"), string.Empty)));

            ComponentRuleListView ruleList = null;

            VisualElement componentSection = Section("Component Icons",
                "Icons are drawn at the right-hand end of the Name column and are cached per GameObject, " +
                "so scrolling never re-scans components.",
                new PropertyField(serialized.FindProperty("m_ComponentIcons"), string.Empty));

            ruleList = new ComponentRuleListView(settings, () =>
            {
                settings.MarkChangedDeferred();
                UpdateComponentDiagnostics(settings, componentDiagnostics);
                DecoratorHost.RequestRefreshAllLiveRows();
            });

            componentSection.Add(new Label("Displayed Components") { name = "hd-subheading" });
            componentSection.Q<Label>("hd-subheading").AddToClassList("hd-subheading");
            componentSection.Add(ruleList);
            componentSection.Add(componentDiagnostics);

            scroll.Add(componentSection);

            scroll.Add(Section("Diagnostics",
                "Kept intentionally cheap: the component scan that feeds the icon strip already produces the " +
                "missing-script count.",
                new PropertyField(serialized.FindProperty("m_Indicators"), string.Empty)));

            scroll.Add(BuildMaintenanceSection(settings, ruleList));

            root.Bind(serialized);

            UpdateHeaderDiagnostics(settings, headerDiagnostics);
            UpdateComponentDiagnostics(settings, componentDiagnostics);

            root.TrackSerializedObjectValue(serialized, _ =>
            {
                // The regex cache is keyed by the pattern string, so an edited pattern is a natural miss -
                // clearing it here would recompile every other rule's pattern on every keystroke.
                settings.MarkChangedDeferred();
                UpdateHeaderDiagnostics(settings, headerDiagnostics);
                UpdateComponentDiagnostics(settings, componentDiagnostics);
                DecoratorHost.RequestRefreshAllLiveRows();
            });
        }

        private static void BuildUserPreferences(VisualElement root)
        {
            HierarchyDecoratorUserSettings settings = HierarchyDecoratorUserSettings.instance;

            ApplyStyle(root);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("hd-settings-root");
            root.Add(scroll);

            Toggle enabled = new Toggle("Enable decorations")
            {
                value = settings.Enabled,
                tooltip = "Turns every decoration off for you only. Project settings are untouched."
            };

            enabled.RegisterValueChangedCallback(evt =>
            {
                settings.Enabled = evt.newValue;
                DecoratorHost.RefreshAllLiveRows();
            });

            Toggle legacyHint = new Toggle("Warn when the legacy Hierarchy window is in use")
            {
                value = settings.ShowLegacyHierarchyHint,
                tooltip = "The Unity 6.6 extension API is never invoked by the legacy window, so no " +
                          "decorations are drawn there."
            };

            legacyHint.RegisterValueChangedCallback(evt => settings.ShowLegacyHierarchyHint = evt.newValue);

            VisualElement section = Section("Personal", "These preferences are stored in UserSettings/ and are not shared with your team.", enabled);
            section.Add(legacyHint);
            scroll.Add(section);

            VisualElement storage = Section("Storage", null, new Label($"Shared settings: {HierarchyDecoratorSettings.FilePath}"));
            storage.Add(new Label($"Personal settings: {HierarchyDecoratorUserSettings.FilePath}"));

            foreach (Label label in storage.Query<Label>().ToList())
            {
                label.AddToClassList("hd-hint");
            }

            scroll.Add(storage);
        }

        private static VisualElement BuildPresetSection(HierarchyDecoratorSettings settings)
        {
            List<string> choices = new List<string>();

            for (int i = 0; i < BuiltInPresets.All.Count; i++)
            {
                choices.Add(BuiltInPresets.All[i].name);
            }

            for (int i = 0; i < settings.CustomPresets.Count; i++)
            {
                choices.Add(settings.CustomPresets[i].name);
            }

            string active = HierarchyDecoratorUserSettings.instance.ActivePreset;

            if (!choices.Contains(active) && choices.Count > 0)
            {
                active = choices[0];
            }

            DropdownField dropdown = new DropdownField("Preset", choices, Mathf.Max(0, choices.IndexOf(active)))
            {
                tooltip = "Presets set the feature toggles. They never overwrite your header or component rules."
            };

            Button apply = new Button(() =>
            {
                Preset preset = FindPreset(settings, dropdown.value);

                if (preset == null)
                {
                    return;
                }

                Undo.RegisterCompleteObjectUndo(settings, "Apply Preset");
                preset.ApplyTo(settings);
                HierarchyDecoratorUserSettings.instance.ActivePreset = preset.name;
                settings.MarkChanged();
                DecoratorHost.RefreshAllLiveRows();
                SettingsService.NotifySettingsProviderChanged();
            })
            {
                text = "Apply"
            };

            Button save = new Button(() =>
            {
                // Derived from Count, the name repeated as soon as anything was deleted - and both Find and
                // the Delete handler take the first match, so Apply and Delete then acted on the wrong one.
                string name = NextCustomPresetName(settings);

                Undo.RegisterCompleteObjectUndo(settings, "Save Preset");
                settings.CustomPresets.Add(Preset.CaptureFrom(settings, name));
                settings.MarkChanged();

                choices.Add(name);
                dropdown.choices = choices;
                dropdown.value = name;
            })
            {
                text = "Save Current As...",
                tooltip = "Captures the current feature toggles as a new project preset."
            };

            Button delete = new Button(() =>
            {
                Preset custom = settings.CustomPresets.Find(p => p.name == dropdown.value);

                if (custom == null)
                {
                    return;
                }

                Undo.RegisterCompleteObjectUndo(settings, "Delete Preset");
                settings.CustomPresets.Remove(custom);
                settings.MarkChanged();

                choices.Remove(custom.name);
                dropdown.choices = choices;
                dropdown.value = choices.Count > 0 ? choices[0] : string.Empty;
            })
            {
                text = "Delete"
            };

            delete.SetEnabled(!BuiltInPresets.IsBuiltIn(dropdown.value));
            dropdown.RegisterValueChangedCallback(evt => delete.SetEnabled(!BuiltInPresets.IsBuiltIn(evt.newValue)));

            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("hd-row");
            buttons.Add(apply);
            buttons.Add(save);
            buttons.Add(delete);

            VisualElement section = Section("Preset", null, dropdown);
            section.Add(buttons);
            return section;
        }

        private static VisualElement BuildMaintenanceSection(HierarchyDecoratorSettings settings, ComponentRuleListView ruleList)
        {
            Button import = new Button(() =>
            {
                EditorApplication.ExecuteMenuItem(PackageInfo.ToolsMenuPath + "Import Settings From 1.x");

                // An import replaces the whole rule list, so the view's cached rule references are stale.
                ruleList?.Refresh();
            })
            {
                text = "Import Settings From 1.x"
            };

            Button reset = new Button(() =>
            {
                EditorApplication.ExecuteMenuItem(PackageInfo.ToolsMenuPath + "Reset Settings To Defaults");
                ruleList?.Refresh();
            })
            {
                text = "Reset To Defaults"
            };

            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("hd-row");
            buttons.Add(import);
            buttons.Add(reset);

            return Section("Maintenance", $"Stored at {HierarchyDecoratorSettings.FilePath}", buttons);
        }

        private static HelpBox CreateDiagnosticsBox()
        {
            HelpBox box = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            box.style.display = DisplayStyle.None;
            return box;
        }

        /// <summary>
        /// Reports header rules whose regex will never match. Without this a typo is completely silent: the
        /// rule simply stops applying, and the matcher deliberately does not log while the user is typing.
        /// </summary>
        private static void UpdateHeaderDiagnostics(HierarchyDecoratorSettings settings, HelpBox box)
        {
            StringBuilder builder = null;

            for (int i = 0; i < settings.HeaderRules.Count; i++)
            {
                HeaderRule rule = settings.HeaderRules[i];

                if (rule == null || !rule.useRegex)
                {
                    continue;
                }

                string error = NameMatcher.ValidateRegex(rule.pattern);

                if (error == null)
                {
                    continue;
                }

                builder ??= new StringBuilder("Invalid regular expression:");
                builder.Append("\n  ").Append(string.IsNullOrEmpty(rule.name) ? "Rule " + i : rule.name)
                    .Append(" - ").Append(error);
            }

            SetDiagnostics(box, builder);
        }

        /// <summary>
        /// Reports component rules whose type no longer resolves. The rule is kept - deleting a user's
        /// configuration because a script was temporarily removed would be worse - but it has to be visible.
        /// </summary>
        private static void UpdateComponentDiagnostics(HierarchyDecoratorSettings settings, HelpBox box)
        {
            StringBuilder builder = null;

            for (int i = 0; i < settings.ComponentRules.Count; i++)
            {
                ComponentRule rule = settings.ComponentRules[i];

                if (rule == null || ComponentCatalog.Resolve(rule) != null)
                {
                    continue;
                }

                builder ??= new StringBuilder("These component rules no longer resolve to a type and are ignored. " +
                                              "They are kept in case the script comes back:");

                builder.Append("\n  ").Append(
                    string.IsNullOrEmpty(rule.typeName) ? "script " + rule.monoScriptGuid : rule.typeName);
            }

            SetDiagnostics(box, builder);
        }

        private static void SetDiagnostics(HelpBox box, StringBuilder builder)
        {
            if (builder == null)
            {
                box.style.display = DisplayStyle.None;
                return;
            }

            box.text = builder.ToString();
            box.style.display = DisplayStyle.Flex;
        }

        private static VisualElement BuildLegacyWarning()
        {
            HelpBox box = new HelpBox(
                "This project is using the legacy Hierarchy window. It does not support the Unity 6.6 " +
                "hierarchy extension API, so no decorations are drawn. Turn off " +
                "Project Settings > Editor > Hierarchy > Use Legacy Hierarchy.",
                HelpBoxMessageType.Warning);

            box.style.display = LegacyHierarchyNotice.IsUsingLegacyWindow() ? DisplayStyle.Flex : DisplayStyle.None;
            return box;
        }

        /// <summary>First unused "Custom N", so two presets can never share a name.</summary>
        private static string NextCustomPresetName(HierarchyDecoratorSettings settings)
        {
            for (int i = 1; i < 1000; i++)
            {
                string candidate = "Custom " + i;

                if (BuiltInPresets.IsBuiltIn(candidate))
                {
                    continue;
                }

                if (settings.CustomPresets.Find(p => p.name == candidate) == null)
                {
                    return candidate;
                }
            }

            return "Custom";
        }

        private static Preset FindPreset(HierarchyDecoratorSettings settings, string name)
        {
            Preset builtIn = BuiltInPresets.Find(name);
            return builtIn ?? settings.CustomPresets.Find(p => p.name == name);
        }

        private static VisualElement Section(string title, string description, VisualElement content)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("hd-section");

            Label heading = new Label(title);
            heading.AddToClassList("hd-heading");
            section.Add(heading);

            if (!string.IsNullOrEmpty(description))
            {
                Label hint = new Label(description);
                hint.AddToClassList("hd-hint");
                section.Add(hint);
            }

            if (content != null)
            {
                section.Add(content);
            }

            return section;
        }

        private static void ApplyStyle(VisualElement root)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageInfo.SettingsStyleSheet);

            if (sheet != null && !root.styleSheets.Contains(sheet))
            {
                root.styleSheets.Add(sheet);
            }
        }
    }
}
