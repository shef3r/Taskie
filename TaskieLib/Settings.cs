﻿using System;
using Windows.Storage;
using Windows.Foundation.Collections;
using Windows.Globalization;
using TaskieLib.Models;
using System.Linq;

namespace TaskieLib
{
    public static class Settings
    {
        private static IPropertySet savedSettings = ApplicationData.Current.LocalSettings.Values;

        public static string Theme
        {
            get
            {
                if (savedSettings.ContainsKey("theme"))
                {
                    string theme = savedSettings["theme"] as string;
                    if (theme == "Light" || theme == "Dark" || theme == "System")
                    {
                        return theme;
                    }
                }
                return "System";
            }
            set
            {
                if (value == "Light" || value == "Dark" || value == "System")
                {
                    savedSettings["theme"] = value;
                }
            }
        }

        public static bool isAuthUsed
        {
            get
            {
                if (savedSettings.ContainsKey("auth") && (string)savedSettings["auth"] == "1")
                {
                    return true;
                }
                return false;
            }
            set
            {
                savedSettings["auth"] = value ? "1" : "0";
            }
        }

        public static bool Launched
        {
            get
            {
                if (savedSettings.ContainsKey("launched") && (string)savedSettings["launched"] == "1")
                {
                    return true;
                }
                return false;
            }
            set
            {
                savedSettings["launched"] = value ? "1" : "0";
            }
        }

        public static bool isPro
        { // peri use a store getter here pls
            get
            {
                if (savedSettings.ContainsKey("pro") && (string)savedSettings["pro"] == "1")
                {
                    return true;
                }
                return false;
            }
            set
            {
                savedSettings["pro"] = value ? "1" : "0";
            }
        }
    }
}
