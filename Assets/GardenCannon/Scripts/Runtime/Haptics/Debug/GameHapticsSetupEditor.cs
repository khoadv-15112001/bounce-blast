#if UNITY_EDITOR || DEVELOPMENT_BUILD || SONAT_CHEAT_RELEASE
using System;
using System.Collections.Generic;
using System.Linq;
using Sonat.Game.Haptics;
using UnityEngine;
using UnityEngine.UIElements;

namespace GardenCannon
{
    /// <summary>Game-owned editor for gameplay haptics configuration and testing.</summary>
    internal sealed class GameHapticsSetupEditor
    {
        private static readonly Color k_Text = new(0.9f, 0.9f, 0.9f);
        private static readonly Color k_Muted = new(0.55f, 0.57f, 0.6f);
        private static readonly Color k_Button = new(0.165f, 0.176f, 0.2f);
        private static readonly Color k_Selected = new(0.094f, 0.373f, 0.647f);
        private static readonly Color k_Border = new(0.23f, 0.24f, 0.27f);
        private static readonly Color k_Card = new(0.12f, 0.13f, 0.15f);
        private static readonly Color k_Field = new(0.11f, 0.12f, 0.135f);

        private static readonly HapticType[] k_Types = (HapticType[])Enum.GetValues(typeof(HapticType));
        private static readonly GameHapticMoment[] k_Moments = GameHapticsMap.DisplayOrder();
        private readonly HashSet<GameHapticMoment> m_Expanded = new();
        private Action m_Refresh;
        private Action m_MapChanged;
        private Action m_ResetMap;
        private Action<HapticTuning, float> m_SoundPreview;
        private string m_OpenChoice;
        private bool m_ResetConfirming;

        public GameHapticMoment SelectedMoment { get; private set; } = GameHapticMoment.ItemBroken;
        public int SelectedCombo { get; private set; } = 1;

        public VisualElement CreateView(Action<HapticTuning, float> soundPreview, Action mapChanged, Action resetMap)
        {
            m_SoundPreview = soundPreview;
            m_MapChanged = mapChanged;
            m_ResetMap = resetMap;

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 2;
            root.style.paddingRight = 2;
            m_Refresh = () =>
            {
                var parent = root.parent;
                if (parent == null) return;
                var index = parent.IndexOf(root);
                parent.RemoveAt(index);
                parent.Insert(index, CreateView(m_SoundPreview, m_MapChanged, m_ResetMap));
            };

            var intro = new Label(Application.isEditor
                ? "GAME MAP  ·  Tự lưu trên thiết bị · Editor phát âm thanh thay rung (Android Composition)"
                : "GAME MAP  ·  Tự lưu trên thiết bị · áp dụng ngay");
            intro.style.color = k_Muted;
            intro.style.fontSize = 10;
            intro.style.marginLeft = 4;
            intro.style.marginBottom = 6;
            root.Add(intro);

            var actions = Row();
            actions.Add(ActionButton(m_ResetConfirming ? "Xác nhận reset map" : "Reset map mặc định game", false, () =>
            {
                if (!m_ResetConfirming)
                {
                    m_ResetConfirming = true;
                    m_Refresh();
                    return;
                }
                m_ResetConfirming = false;
                m_ResetMap?.Invoke();
                m_Refresh();
            }));
            actions.Add(ActionButton("Hủy", false, () =>
            {
                m_ResetConfirming = false;
                m_Refresh();
            }));
            actions.Add(ActionButton("Mở tất cả", false, () =>
            {
                foreach (var moment in k_Moments) m_Expanded.Add(moment);
                m_Refresh();
            }));
            actions.Add(ActionButton("Dump map to Console", false, () =>
                Debug.Log($"GameHapticsMap:\n{JsonUtility.ToJson(GameHapticsMap.Current, true)}")));
            root.Add(actions);

            var map = GameHapticsMap.Current;
            foreach (var moment in k_Moments)
                root.Add(MomentCard(map, moment));
            root.Add(ComboCard(map));
            return root;
        }

        public void PlaySelected() => Play(SelectedMoment, SelectedCombo);

        private void MarkChanged() => m_MapChanged?.Invoke();

        private VisualElement MomentCard(GameHapticsMap map, GameHapticMoment moment)
        {
            var entry = map.Find(moment);
            var card = Card();
            var header = Row();
            header.style.alignItems = Align.Center;

            var expanded = m_Expanded.Contains(moment);
            var title = ActionButton($"{(expanded ? "⌄" : "›")}  {MomentLabel(moment)}", false, () =>
            {
                if (!m_Expanded.Add(moment)) m_Expanded.Remove(moment);
                m_Refresh();
            });
            title.style.flexGrow = 1;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.backgroundColor = Color.clear;
            title.style.borderTopWidth = title.style.borderBottomWidth = title.style.borderLeftWidth = title.style.borderRightWidth = 0;
            header.Add(title);

            var typeLabel = new Label(entry.preset != null ? entry.preset.name : entry.type.ToString());
            typeLabel.style.color = k_Muted;
            typeLabel.style.fontSize = 10;
            typeLabel.style.marginRight = 5;
            header.Add(typeLabel);

            header.Add(ActionButton(entry.enabled ? "ON" : "OFF", entry.enabled, () =>
            {
                map.Row(moment).enabled = !map.Row(moment).enabled;
                SelectedMoment = moment;
                MarkChanged();
                m_Refresh();
            }));
            header.Add(ActionButton("▶", false, () => Play(moment)));
            card.Add(header);

            if (!expanded) return card;

            if (entry.preset == null)
                card.Add(TypeChoices(map, moment, entry));
            card.Add(IntensityControl("Cường độ", entry.intensity, 0f, 1f, value =>
            {
                map.Row(moment).intensity = value;
                SelectedMoment = moment;
                MarkChanged();
            }));
            card.Add(GapControl(entry.minGapMs, value =>
            {
                map.Row(moment).minGapMs = value;
                SelectedMoment = moment;
                MarkChanged();
            }));
            card.Add(PresetChoices(map, moment, entry));
            return card;
        }

        private VisualElement TypeChoices(GameHapticsMap map, GameHapticMoment moment, GameHapticEntry entry)
        {
            return ChoiceDropdown("HAPTIC TYPE", $"type-{moment}",
                Array.ConvertAll(k_Types, type => type.ToString()), (int)entry.type, index =>
                {
                    var row = map.Row(moment);
                    row.type = (HapticType)index;
                    row.preset = null;
                    SelectedMoment = moment;
                    MarkChanged();
                    m_Refresh();
                });
        }

        private VisualElement PresetChoices(GameHapticsMap map, GameHapticMoment moment, GameHapticEntry entry)
        {
            var presets = Presets(entry.preset);
            var choices = new[] { "None" }.Concat(presets.Select(preset => preset.name)).ToArray();
            var selected = entry.preset == null ? 0 : presets.IndexOf(entry.preset) + 1;
            return ChoiceDropdown("PRESET  ·  overrides type", $"preset-{moment}", choices, selected, index =>
                {
                    map.Row(moment).preset = index == 0 ? null : presets[index - 1];
                    SelectedMoment = moment;
                    MarkChanged();
                    m_Refresh();
                });
        }

        private VisualElement ComboCard(GameHapticsMap map)
        {
            var card = Card();
            var header = Row();
            header.Add(SectionTitle("ITEM BROKEN  ·  COMBO"));
            header.Add(ActionButton("▶  combo 5", false, () =>
            {
                Play(GameHapticMoment.ItemBroken, 5);
            }));
            card.Add(header);

            card.Add(IntensityControl("Combo base", map.comboBase, 0f, 1f, value =>
            {
                map.comboBase = value;
                MarkChanged();
            }));
            card.Add(IntensityControl("Combo step", map.comboStep, 0f, 0.5f, value =>
            {
                map.comboStep = value;
                MarkChanged();
            }));
            card.Add(GapControl(map.comboPresetFrom, value =>
            {
                map.comboPresetFrom = Mathf.Max(1, value);
                MarkChanged();
            },
                "Dùng preset từ combo", minimum: 1, step: 1));
            card.Add(ComboPresetChoices(map));
            return card;
        }

        private VisualElement ComboPresetChoices(GameHapticsMap map)
        {
            var presets = Presets(map.comboPreset);
            var choices = new[] { "None" }.Concat(presets.Select(preset => preset.name)).ToArray();
            var selected = map.comboPreset == null ? 0 : presets.IndexOf(map.comboPreset) + 1;
            return ChoiceDropdown("COMBO PRESET  ·  optional", "combo-preset", choices, selected, index =>
                {
                    map.comboPreset = index == 0 ? null : presets[index - 1];
                    MarkChanged();
                    m_Refresh();
                });
        }

        private VisualElement ChoiceDropdown(string title, string key, string[] choices, int selected,
            Action<int> picked)
        {
            var section = Section(title);
            if (choices.Length == 0) return section;

            selected = Mathf.Clamp(selected, 0, choices.Length - 1);
            var open = m_OpenChoice == key;
            section.Add(ActionButton($"{choices[selected]}  {(open ? "▴" : "▾")}", false, () =>
            {
                m_OpenChoice = open ? null : key;
                m_Refresh();
            }));

            if (!open) return section;

            var options = new VisualElement();
            options.style.marginTop = 2;
            options.style.paddingLeft = options.style.paddingRight = 4;
            options.style.paddingTop = options.style.paddingBottom = 3;
            options.style.backgroundColor = k_Field;
            SetBorder(options, k_Border, 6);
            for (var i = 0; i < choices.Length; i++)
            {
                var index = i;
                var option = ActionButton(choices[i], i == selected, () =>
                {
                    m_OpenChoice = null;
                    picked(index);
                });
                option.style.flexGrow = 1;
                options.Add(option);
            }
            section.Add(options);
            return section;
        }

        private VisualElement IntensityControl(string title, float value, float min, float max, Action<float> changed)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            var label = new Label(title);
            label.style.width = 92;
            label.style.flexShrink = 0;
            label.style.color = k_Muted;
            label.style.fontSize = 11;
            row.Add(label);

            var slider = new Slider(min, max) { value = value };
            slider.style.flexGrow = 1;
            slider.style.height = 26;
            slider.style.marginLeft = slider.style.marginRight = 3;
            StyleSlider(slider);

            var number = new Label($"{value:0.00}");
            number.style.width = 34;
            number.style.color = k_Text;
            number.style.fontSize = 11;
            number.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.RegisterValueChangedCallback(change =>
            {
                number.text = $"{change.newValue:0.00}";
                changed(change.newValue);
            });
            row.Add(slider);
            row.Add(number);
            return row;
        }

        private VisualElement GapControl(int value, Action<int> changed, string title = "Khoảng lặp", int minimum = 0, int step = 50)
        {
            var row = Row();
            row.style.alignItems = Align.Center;
            var label = new Label(title);
            label.style.width = 92;
            label.style.flexShrink = 0;
            label.style.color = k_Muted;
            label.style.fontSize = 11;
            row.Add(label);

            var stepper = new VisualElement();
            stepper.style.flexDirection = FlexDirection.Row;
            stepper.style.alignItems = Align.Center;
            var current = Mathf.Max(minimum, value);
            var number = new Label($"{current} ms");
            number.style.minWidth = 58;
            number.style.color = k_Text;
            number.style.unityTextAlign = TextAnchor.MiddleCenter;
            void Apply(int delta)
            {
                current = Mathf.Max(minimum, current + delta);
                number.text = $"{current} ms";
                changed(current);
            }
            stepper.Add(ActionButton("−", false, () => Apply(-step)));
            stepper.Add(number);
            stepper.Add(ActionButton("+", false, () => Apply(step)));
            row.Add(stepper);
            return row;
        }

        private VisualElement Section(string title)
        {
            var section = new VisualElement();
            section.style.marginTop = 4;
            section.style.marginBottom = 3;
            var label = SectionTitle(title);
            section.Add(label);
            return section;
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.style.color = k_Muted;
            label.style.fontSize = 9;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 4;
            label.style.marginBottom = 2;
            return label;
        }

        private void Play(GameHapticMoment moment)
        {
            Play(moment, 1);
        }

        private void Play(GameHapticMoment moment, int combo)
        {
            SelectedMoment = moment;
            SelectedCombo = combo;
            if (!Application.isEditor)
            {
                GameHaptics.Play(moment, combo);
                return;
            }

            var map = GameHapticsMap.Current;
            var entry = map.Find(moment);
            if (entry == null || !entry.enabled) return;

            var preset = entry.preset;
            var intensity = entry.intensity;
            if (moment == GameHapticMoment.ItemBroken)
            {
                if (map.comboPreset != null && combo >= map.comboPresetFrom)
                {
                    preset = map.comboPreset;
                    intensity = 1f;
                }
                else
                {
                    // Same formula as GameHaptics.Play.
                    intensity = Mathf.Clamp01(entry.intensity * (map.comboBase + map.comboStep * Mathf.Max(0, combo)));
                }
            }

            var tuning = preset != null ? preset.tuning : HapticsConfig.Current.TuningOf(entry.type);
            m_SoundPreview?.Invoke(tuning, intensity * HapticsConfig.Current.intensity);
        }

        private static List<HapticPreset> Presets(HapticPreset current)
        {
            var result = new List<HapticPreset>();
            var config = HapticsConfig.Current.presets ?? Array.Empty<HapticPreset>();
            foreach (var preset in config)
                if (preset != null && !result.Contains(preset)) result.Add(preset);
            if (current != null && !result.Contains(current)) result.Add(current);
            return result;
        }

        private static string MomentLabel(GameHapticMoment moment) =>
            moment switch
            {
                GameHapticMoment.AimTick => "Aim tick",
                GameHapticMoment.FireRefused => "Fire refused",
                GameHapticMoment.LastMoves => "Last 5 moves",
                GameHapticMoment.WallBounce => "Wall bounce",
                GameHapticMoment.ItemHit => "Solid item hit",
                GameHapticMoment.ItemBroken => "Item broken (combo)",
                GameHapticMoment.BigItemBroken => "Big item broken",
                GameHapticMoment.ObstacleStep => "Box opened",
                GameHapticMoment.GoalHit => "Goal hit",
                GameHapticMoment.GoalComplete => "Goal complete",
                GameHapticMoment.BallSplash => "Ball splash",
                GameHapticMoment.LevelWon => "Level won",
                GameHapticMoment.WinFireworks => "Win fireworks",
                GameHapticMoment.LevelLost => "Level lost",
                GameHapticMoment.UiButton => "UI button",
                GameHapticMoment.VibrationTurnedOn => "Vibration on",
                GameHapticMoment.PartBroken => "Crate / log broken",
                GameHapticMoment.OrbActivated => "Orb lit",
                GameHapticMoment.FuseLit => "Fuse lit",
                GameHapticMoment.EggHatched => "Egg hatched",
                GameHapticMoment.OrbsReset => "Orbs reset",
                GameHapticMoment.OrbsDischarged => "Orbs discharged",
                GameHapticMoment.ItemBlocked => "Obstacle blocked",
                GameHapticMoment.ItemDamaged => "Item damaged (no break)",
                GameHapticMoment.VolleyBall => "Volley ball 2..N",
                GameHapticMoment.AimStart => "Aim start (touch)",
                GameHapticMoment.LevelStart => "Level start",
                GameHapticMoment.TutorialShown => "Tutorial shown",
                GameHapticMoment.PopupShown => "Win/Lose popup",
                GameHapticMoment.LastMove => "Last move (1 left)",
                GameHapticMoment.ComboMilestone => "Combo 5/10/15",
                GameHapticMoment.ShotEnded => "Shot ended",
                _ => moment.ToString()
            };

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.marginTop = 3;
            card.style.marginBottom = 3;
            card.style.paddingLeft = card.style.paddingRight = 8;
            card.style.paddingTop = card.style.paddingBottom = 6;
            card.style.backgroundColor = k_Card;
            SetBorder(card, k_Border, 8);
            return card;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static Button Chip(string text, bool selected, Action clicked)
        {
            var button = ActionButton(text, selected, clicked);
            button.style.height = 24;
            button.style.marginLeft = button.style.marginRight = 2;
            button.style.marginTop = button.style.marginBottom = 2;
            button.style.paddingLeft = button.style.paddingRight = 7;
            button.style.fontSize = 10;
            return button;
        }

        private static Button ActionButton(string text, bool selected, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 25;
            button.style.marginLeft = button.style.marginRight = 3;
            button.style.marginTop = button.style.marginBottom = 2;
            button.style.paddingLeft = button.style.paddingRight = 8;
            button.style.fontSize = 10;
            button.style.color = k_Text;
            button.style.backgroundColor = selected ? k_Selected : k_Button;
            SetBorder(button, k_Border, 6);
            return button;
        }

        private static void StyleSlider(Slider slider)
        {
            slider.style.backgroundColor = Color.clear;
            var tracker = slider.Q(className: BaseSlider<float>.trackerUssClassName);
            if (tracker != null)
            {
                tracker.style.height = 4;
                tracker.style.backgroundColor = k_Field;
                SetBorder(tracker, k_Border, 3);
            }
            var dragger = slider.Q(className: BaseSlider<float>.draggerUssClassName);
            if (dragger != null)
            {
                dragger.style.width = 14;
                dragger.style.height = 14;
                dragger.style.backgroundColor = k_Text;
                SetBorder(dragger, k_Selected, 7);
            }
        }

        private static void SetBorder(VisualElement element, Color color, float radius)
        {
            element.style.borderTopWidth = element.style.borderBottomWidth =
                element.style.borderLeftWidth = element.style.borderRightWidth = 1;
            element.style.borderTopColor = element.style.borderBottomColor =
                element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }
    }
}
#endif
