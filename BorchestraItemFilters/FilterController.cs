using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.IO;
using System.Linq;

namespace BorchestraItemFilters
{
    [HarmonyPatch]
    public static class FilterController
    {
        [SerializeField]
        static List<string> ItemsToFilter = new List<string>();

        public const string FiltersFileName = "Filters.txt";
        public static string FiltersFilePath;
        public static Action<IEnumerable<string>, IEnumerable<string>> OnFiltersChanged;

        public static void AddItemToFilterList(string itemName)
        {
            ItemsToFilter.Add(itemName);
            OnFiltersChanged?.Invoke([itemName], []);
        }
        public static void AddRangeToFilterList(IEnumerable<string> strings)
        {
            ItemsToFilter.AddRange(strings);
            OnFiltersChanged?.Invoke(strings, []);
        }
        public static void RemoveItemFromFilterList(string itemName)
        {
            ItemsToFilter.Remove(itemName);
            OnFiltersChanged?.Invoke([], [itemName]);
        }

        public static string[] GetAllFilteredItems()
            => ItemsToFilter.ToArray();
        public static bool ItemIsFiltered(string itemName)
            => ItemsToFilter.Contains(itemName);

        public static void ClearFilters()
        {
            string[] oldFilters = ItemsToFilter.ToArray();
            ItemsToFilter.Clear();
            OnFiltersChanged?.Invoke([],oldFilters);
        }

        public static void Init()
        {
            FiltersFilePath = Path.Combine(Paths.ConfigPath, FiltersFileName);
            OnFiltersChanged += SaveFilters;
            LoadFilters();
        }

        public static void LoadFilters()
        {
            ItemsToFilter.Clear();

            if (!File.Exists(FiltersFilePath))
                return;

            foreach (string itm in File.ReadAllLines(FiltersFilePath))
                ItemsToFilter.Add(itm.Trim());

            Debug.Log("Filters loaded successfully.");
        }
        public static void SaveFilters(IEnumerable<string> addedItems, IEnumerable<string> removedItems)
        {
            var dirName = Path.GetDirectoryName(FiltersFilePath);

            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            File.WriteAllLines(FiltersFilePath, ItemsToFilter);
        }
        [HarmonyPatch(typeof(ItemPoolDataBase), nameof(ItemPoolDataBase.TryGetPrizeItem))]
        [HarmonyPatch(typeof(ItemPoolDataBase), nameof(ItemPoolDataBase.TryGetShopItem))]
        [HarmonyILManipulator]
        public static void ItemOverride_Transpiler(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            if (!cursor.JumpToNext(instr => instr.MatchCall<UnityEngine.Object>("op_Equality")))
                return;

            cursor.Emit(OpCodes.Ldloc_2);
            cursor.Emit(OpCodes.Call, AccessTools.Method(typeof(FilterController), nameof(ModifyValidItem)));
        }

        static bool ModifyValidItem(bool validItem, string itemName)
        {
            //DebugController.Instance.WriteLine("TEMP BUT IT WORKS!!!!", BepInEx.Logging.LogLevel.Info);
            //if (ItemIsFiltered(itemName)) Debug.Log($"{itemName} WAS SUCCESSFULLY FILTERED!, should return {validItem && ItemIsFiltered(itemName)} as the check on this part exits when true.");

            return validItem || ItemIsFiltered(itemName);
        }


    }
}
