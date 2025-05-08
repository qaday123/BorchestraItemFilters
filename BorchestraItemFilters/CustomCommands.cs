using BepInEx.Logging;
using BrutalAPI;
using System.Linq;
using static BrutalAPI.DebugController;

namespace BorchestraItemFilters
{
    public static class CustomCommands
    {
        public static DebugCommand FILTER
        { get; private set; }
        public static DebugCommand FILTERLIST
        { get; private set; }
        public static DebugCommand CLEARFILTERS;
        public static void AddCommands()
        {
            FILTER = new DebugCommand("filter", "Toggles whether an item is being filtered from a run or not.", new()
            {
                new StringCommandArgument("item", DebugController.ItemAutocomplete, false)
            }, args =>
            {
                foreach (var arg in args)
                {
                    if (!arg.TryRead(out string itmName) || string.IsNullOrEmpty(itmName))
                    { 
                        continue; 
                    }
                    var itm = LoadedAssetsHandler.GetWearable(itmName);

                    if (itm == null)
                    {
                        Instance.WriteLine($"Unknown item \"{itmName}\".", LogLevel.Error);

                        return;
                    }

                    if (FilterController.ItemIsFiltered(itmName))
                    {
                        FilterController.RemoveItemFromFilterList(itmName);
                        Instance.WriteLine($"{itmName} is no longer being filtered.");
                    }
                    else
                    {
                        FilterController.AddItemToFilterList(itmName);
                        Instance.WriteLine($"{itmName} is now being filtered.");
                    }
                }
            }, true);

            FILTERLIST = new DebugCommand("filterlist", "Lists all items currently being filtered.", new()
            {

            }, args =>
            {
                string message = "Items being fitered: ";
                string[] items = FilterController.GetAllFilteredItems();
                
                if (items.Length == 0)
                {
                    Instance.WriteLine("No items currently being filtered.");
                    return;
                }

                foreach (string itm in items)
                {
                    message += itm + ", ";
                }
                
                message = message.Remove(message.Length - 2, 2);
                Instance.WriteLine(message);
            });

            CLEARFILTERS = new DebugCommand("filterclear", "Clears all currently active filters.", [],
                args =>
                {
                    FilterController.ClearFilters();
                    Instance.WriteLine("Filters cleared.");
                });

            Commands.children.Add(FILTER);
            Commands.children.Add(FILTERLIST);
            Commands.children.Add(CLEARFILTERS);

            /*var filterAllGroup = new DebugCommandGroup("filterall", "[DEBUG] filter every single item in its category except one. Debug purposes only.")
            {
                children = new()
                {
                    new DebugCommand("shop", "filters all but 1 shop item", [], args =>
                    {
                        FilterAllButOneInList(LoadedDBsHandler.ItemUnlocksDB.ShopItems, "Shop");
                    }),
                    new DebugCommand("treasure", "filters all but 1 treasure item", [], args =>
                    {
                        FilterAllButOneInList(LoadedDBsHandler.ItemUnlocksDB.TreasureItems, "Treasure");
                    }),
                    new DebugCommand("extras", "i think this is like the fishing rod pool and stuff", [], args =>
                    {
                        FilterAllButOneInList(LoadedDBsHandler.ItemUnlocksDB.ExtraItems, "Extras");
                    }),
                    new DebugCommand("all", "Filters all but 1 item. uhhh yeah sure.", [], args =>
                    {
                        FilterAllButOneInList(LoadedAssetsHandler.LoadedWearables.Keys.ToArray(), "Modded?");
                    })
                }
            };
            Commands.children.Add(filterAllGroup);//*/
        }
        private static void FilterAllButOneInList(ItemUnlockInfo[] itemList, string categoryName)
        {
            string[] nameList = new string[itemList.Length];
            for (int i = 0; i < itemList.Length; i++)
            {
                nameList[i] = itemList[i].itemName;
            }
            FilterAllButOneInList(nameList, categoryName);
        }
        private static void FilterAllButOneInList(string[] itemIDs, string categoryName)
        {
            var listToRemoveFrom = itemIDs.ToList();
            int index = UnityEngine.Random.Range(0, itemIDs.Length);
            string survivor = listToRemoveFrom[index];
            listToRemoveFrom.RemoveAt(index);

            foreach (string itm in listToRemoveFrom)
            {
                FilterController.AddItemToFilterList(itm);
            }

            Instance.WriteLine($"All items in the {categoryName} category filtered except for {survivor}", LogLevel.Debug);
        }
    }
}
