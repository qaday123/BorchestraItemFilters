using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BrutalAPI;

namespace BorchestraItemFilters
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "Qaday.ItemFilters";
        public const string NAME = "Item Filters";
        public const string VERSION = "1.0.0";

        public void Start()
        {
            var harmony = new Harmony(GUID);
            harmony.PatchAll();

            FilterController.Init();
            CustomCommands.AddCommands();
            UIHandler.Init();
            Debug.Log("Tradition to make sure this mod is loading.");
        }
    }
}