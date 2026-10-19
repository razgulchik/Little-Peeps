using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The build panel's tab column: a button per non-empty BuildPaletteDef tab, one of them open. It owns
    // WHICH tab is open and announces every change (BuildTabChangedEvent) — the palette shows that tab's
    // cards, and nothing here knows the palette exists. Digit N (BuildTabRequestedEvent) opens the N-th
    // button, in normal play only; bringing a hidden panel back for it is the build toggle's business.
    //
    // The open tab is UI state, not run state: it survives other screens and prestiges.
    public class BuildTabBar : MonoBehaviour
    {
        [SerializeField] private BuildPaletteDef palette;
        [SerializeField] private BuildTabUI tabPrefab;

        [Tooltip("Parent of the spawned tab buttons, the one with the Layout Group. Empty = this object.")]
        [SerializeField] private Transform container;

        private readonly List<BuildTab> tabs = new();         // the non-empty ones: palette order = digit order
        private readonly List<BuildTabUI> buttons = new();    // same index; null while no tab prefab is wired
        private int current = -1;

        // Boots live, for the reason BuildPaletteUI gives.
        private bool live = true;

        private void OnEnable()
        {
            EventBus<BuildTabRequestedEvent>.Subscribe(OnTabHotkey);
            EventBus<UIModeChangedEvent>.Subscribe(OnUIMode);
        }

        private void OnDisable()
        {
            EventBus<BuildTabRequestedEvent>.Unsubscribe(OnTabHotkey);
            EventBus<UIModeChangedEvent>.Unsubscribe(OnUIMode);
        }

        // In Start, not Awake: the first tab is announced here, and by now every listener has subscribed
        // (OnEnable runs with Awake, before any Start).
        private void Start()
        {
            if (palette == null)
            {
                Debug.LogWarning("[BuildTabBar] no Palette assigned — no tabs, and so no cards.", this);
                return;
            }

            Transform parent = container != null ? container : transform;
            foreach (var tab in palette.tabs)
            {
                if (tab == null || !HasStructures(tab)) continue;   // an empty tab gets no button and no digit

                BuildTabUI button = null;
                if (tabPrefab != null)
                {
                    button = Instantiate(tabPrefab, parent);
                    button.Init(tab, tabs.Count, Open);
                }

                tabs.Add(tab);
                buttons.Add(button);
            }

            Open(0);
        }

        private static bool HasStructures(BuildTab tab)
        {
            foreach (var def in tab.structures)
                if (def != null) return true;
            return false;
        }

        private void Open(int index)
        {
            if (index < 0 || index >= tabs.Count || index == current) return;

            if (current >= 0 && buttons[current] != null) buttons[current].SetSelected(false);
            current = index;
            if (buttons[current] != null) buttons[current].SetSelected(true);

            EventBus<BuildTabChangedEvent>.Publish(new BuildTabChangedEvent { Tab = tabs[current] });
        }

        // Over another screen the digits do nothing: the tab stays as the player left it.
        private void OnTabHotkey(BuildTabRequestedEvent e)
        {
            if (live) Open(e.Index);
        }

        // Leaving normal play with the pointer resting on a tab: the hidden column may never be told the
        // pointer left, so each tab drops its hover and snaps to where it belongs.
        private void OnUIMode(UIModeChangedEvent e)
        {
            bool nowLive = e.Mode == UIMode.Playing;
            if (nowLive == live) return;
            live = nowLive;

            if (!live)
                foreach (var button in buttons)
                    if (button != null) button.ResetHover();
        }
    }
}
