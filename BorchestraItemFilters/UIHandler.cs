using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using Mono.Cecil.Cil;
using System.IO;
using System.Reflection;
using static Mono.Cecil.Cil.OpCodes;
using TMPro;
using UnityEngine.EventSystems;
using FMODUnity;

namespace BorchestraItemFilters
{
    [HarmonyPatch]
    public static class UIHandler
    {
        public static Dictionary<string, UnlockIconUILayout> itemNameIconMap = new Dictionary<string, UnlockIconUILayout>();
        public static List<string> itemsToDefilter = new List<string>();
        public static UnlockedItemsUIHandler itemStatsUI;
        public static FilterUIPanel filterUI;
        public static void Init()
        {
            FilterController.OnFiltersChanged += UpdateItemPanel;
        }
        public static void SetupMenus(MainMenuController mainmenu)
        {
            itemStatsUI = mainmenu._unlockItemsMenu;

            string newObjectName = "BGUIFilterText";
            GameObject obj = new GameObject(newObjectName);

            obj.transform.parent = itemStatsUI._extraPanel.transform;
            obj.transform.localPosition = new Vector3(0, -300, 0);
            obj.layer = 5;
            filterUI = obj.AddComponent<FilterUIPanel>();

            var text = filterUI.gameObject.GetComponent<TextMeshProUGUI>();
            if (text == null) text = filterUI.gameObject.AddComponent<TextMeshProUGUI>();

            filterUI.Filtertext = text;
            text.font = LoadedDBsHandler.LocalisationDB.m_DefaultGameFont;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 35;
        }

        [HarmonyPatch(typeof(MainMenuController), nameof(MainMenuController.Start))]
        [HarmonyPrefix]
        public static void OnMainMenuStart_Prefix(MainMenuController __instance)
        {
            itemNameIconMap.Clear();
            SetupMenus(__instance);
        }

        [HarmonyPatch(typeof(UnlockedItemsUIHandler), nameof(UnlockedItemsUIHandler.OpenUnlockMenu))]
        [HarmonyPrefix]
        public static void OnOpenUnlockMenu(UnlockedItemsUIHandler __instance)
        {
            UpdateItemPanel();
        }
        public static void UpdateItemPanel() => UpdateItemPanel([], []);
        public static void UpdateItemPanel(IEnumerable<string> addedItems, IEnumerable<string> removedItems)
        {
            foreach (string item in FilterController.GetAllFilteredItems())
            {
                if (!itemNameIconMap.ContainsKey(item) || itemNameIconMap[item] == null)
                    continue;

                UnlockIconUILayout icon = itemNameIconMap[item];
                icon._image.color = new Color(icon._image.color.r, icon._image.color.g, icon._image.color.b, 0.3f);

                UnlockDataTracker tracker = icon.gameObject.GetComponent<UnlockDataTracker>();
                if (tracker == null)
                    continue;
                tracker.isFiltered = true;
            }
            foreach (string item in removedItems)
            {
                if (!itemNameIconMap.ContainsKey(item) || FilterController.ItemIsFiltered(item))
                    continue;
                UnlockIconUILayout icon = itemNameIconMap[item];
                icon._image.color = new Color(icon._image.color.r, icon._image.color.g, icon._image.color.b, 1f);

                UnlockDataTracker tracker = icon.gameObject.GetComponent<UnlockDataTracker>();
                if (tracker == null)
                    continue;
                tracker.isFiltered = false;

            }
        }

        [HarmonyPatch(typeof(UnlockCategoryUIPanel), nameof(UnlockCategoryUIPanel.TryInitializeUnlockableItem))]
        [HarmonyILManipulator]
        public static void AttachItemTrackers_Transpiler(ILContext ctx)
        {
            ILCursor cx = new ILCursor(ctx);

            if (!cx.JumpToNext(instr => instr.MatchStloc(2)))
                return;

            cx.Emit(OpCodes.Ldarg_3); // ItemUnlockInfo[]
            cx.Emit(OpCodes.Ldloc_3); // itemIndex

            cx.Emit(OpCodes.Ldarg_0); // panel object
            cx.Emit(OpCodes.Ldloc_1); // LineIndex
            cx.Emit(OpCodes.Ldloc_2); // indexInLine

            cx.Emit(OpCodes.Call, AccessTools.Method(typeof(UIHandler), nameof(AttachTrackerToBasegameItem)));
        }
        [HarmonyPatch(typeof(UnlockCategoryUIPanel), nameof(UnlockCategoryUIPanel.TryInitializeUnlockableModdedItem))]
        [HarmonyILManipulator]
        public static void AttachModdedItemTrackers_Transpiler(ILContext ctx)
        {
            ILCursor cx = new ILCursor(ctx);

            if (!cx.JumpToNext(instr => instr.MatchStloc(5)))
                return;

            cx.Emit(Ldloc_0); // Unlocked Modded Item List
            cx.Emit(Ldloc, 8); // itemIndex

            cx.Emit(Ldarg_0); // panel
            cx.Emit(Ldloc, 4); // lineIndex
            cx.Emit(Ldloc, 5); // indexInLine

            cx.Emit(Call, AccessTools.Method(typeof(UIHandler), nameof(AttachTrackerToModdedItem)));

            if (!cx.JumpToNext(instr => instr.MatchStloc(5)))
                return;
                
            cx.Emit(Ldloc_1); // Locked Modded Item List
            cx.Emit(Ldloc, 10); // itemIndex

            cx.Emit(Ldarg_0); //panel
            cx.Emit(Ldloc, 4); // lineIndex
            cx.Emit(Ldloc, 5); // indexInLine

            cx.Emit(Call, AccessTools.Method(typeof(UIHandler), nameof(AttachTrackerToModdedItem)));
        }
        public static void AttachTrackerToIcon(string name, UnlockCategoryUIPanel panel, int lineIndex, int indexInLine, bool isLocked)
        {
            UnlockIconUILayout[] iconsInLine = panel._IconLines[lineIndex].m_IconsInLine;
            UnlockIconUILayout icon = iconsInLine[indexInLine];

            UnlockDataTracker tracker = icon.gameObject.AddComponent<UnlockDataTracker>();
            tracker.itemName = name;
            tracker.uiText = filterUI;
            tracker.isLocked = isLocked;

            icon._image.color = new Color(icon._image.color.r, icon._image.color.g, icon._image.color.b, 1f);

            if (!itemNameIconMap.ContainsKey(name))
                itemNameIconMap.Add(name, icon);
        }
        public static void AttachTrackerToBasegameItem(ItemUnlockInfo[] info, int itemIndex, UnlockCategoryUIPanel panel, int lineIndex, int indexInLine)
        {
            string itemName = info[itemIndex].itemName;
            AttachTrackerToIcon(itemName, panel, lineIndex, indexInLine, !info[itemIndex].IsItemUnlocked);
        }

        public static void AttachTrackerToModdedItem(List<ItemModdedUnlockInfo> moddedItems, int itemIndex, UnlockCategoryUIPanel panel, int lineIndex, int indexInLine)
        {
            string itemName = moddedItems[itemIndex].itemID;
            AttachTrackerToIcon(itemName, panel, lineIndex, indexInLine, !moddedItems[itemIndex].IsItemUnlocked);
        }

    }
    public class UnlockDataTracker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public string itemName;
        public bool isFiltered = false;
        public string soundEvent = "event:/UI/UI_Confirm";//"event:/UI/UI_GEN_Click";
        public FilterUIPanel uiText;
        public bool isLocked;
        public void Start()
        {
            FilterController.OnFiltersChanged += OnFiltersChanged;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (isLocked)
                return;

            RuntimeManager.PlayOneShot(soundEvent, default(Vector3));
            if (!isFiltered)
            {
                FilterController.AddItemToFilterList(itemName);
                uiText.SetFilteredText();
            }
            else if (isFiltered)
            {
                FilterController.RemoveItemFromFilterList(itemName);
                uiText.SetUnfilteredText();
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (uiText == null)
                return;

            if (isLocked)
            {
                uiText.Hide();
                return;
            }
            uiText.Show();
            if (isFiltered)
                uiText.SetFilteredText();
            else
                uiText.SetUnfilteredText();
        }
        public void OnFiltersChanged(IEnumerable<string> addedItems, IEnumerable<string> removedItems)
        {
            isFiltered = FilterController.ItemIsFiltered(itemName);
        }
    }
    public class FilterUIPanel : MonoBehaviour
    {
        public TextMeshProUGUI Filtertext;
        public void SetFilteredText()
        {
            if (Filtertext == null)
                return ;

            Filtertext.text = "This item is currently being filtered\nand will not appear in new runs.\n\nClick to unfilter.";
            Filtertext.color = Color.red;
        }
        public void SetUnfilteredText()
        {
            if (Filtertext == null)
               return;

            Filtertext.text = "This item is not currently being filtered\nand will appear in new runs.\n\nClick to filter.";
            Filtertext.color = Color.green;
        }
        public void Hide()
            => Filtertext.gameObject.SetActive(false);
        public void Show()
            => Filtertext.gameObject.SetActive(true);
        
    }
}
