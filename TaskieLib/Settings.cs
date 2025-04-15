﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using Windows.Storage;

namespace TaskieLib {
    public static class Settings {
        private static IPropertySet savedSettings = ApplicationData.Current.LocalSettings.Values;

        // Application theme (light/dark)
        public static string Theme {
            get {
                if (savedSettings.ContainsKey("theme")) {
                    var theme = savedSettings["theme"] as string;
                    if (theme == "Light" || theme == "Dark" || theme == "System") {
                        return theme;
                    }
                }
                return "System";
            }
            set {
                if (value == "Light" || value == "Dark" || value == "System") {
                    savedSettings["theme"] = value;
                }
            }
        }

        // Authentication via Windows Hello (Pro only)
        public static bool isAuthUsed {
            get {
                return savedSettings.ContainsKey("auth") && (string)savedSettings["auth"] == "1";
            }
            set {
                savedSettings["auth"] = value ? "1" : "0";
            }
        }

        // Gets/sets whether the app has been launched, used for the OOBE and tips.
        public static bool Launched {
            get {
                return savedSettings.ContainsKey("launched") && (string)savedSettings["launched"] == "1";
            }
            set {
                savedSettings["launched"] = value ? "1" : "0";
            }
        }

        private static StoreContext context;

        public static async Task<bool> CheckIfProAsync() {
            if (context == null) {
                context = StoreContext.GetDefault();
            }

            string[] productKinds = { "Durable" };
            var filterList = new List<string>(productKinds);

            StoreProductQueryResult queryResult = await context.GetUserCollectionAsync(filterList);

            if (queryResult.ExtendedError != null) {
                Debug.WriteLine("[Store status] Error: " + queryResult.ExtendedError.Message);
                return false;
            }

            string productId = "9N7T6N7R39NR";
            return queryResult.Products.ContainsKey(productId);
        }
    }
}
