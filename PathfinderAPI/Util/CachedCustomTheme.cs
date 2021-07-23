﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hacknet;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Pathfinder.Util.XML;

namespace Pathfinder.Util
{
    [HarmonyPatch]
    public class CachedCustomTheme : IDisposable
    {
        private static readonly Dictionary<string, AccessTools.FieldRef<OS, Color>> OSColorFieldsFast = new Dictionary<string, AccessTools.FieldRef<OS, Color>>();
        
        [Initialize]
        internal static void Initialize()
        {
            foreach (var field in AccessTools.GetDeclaredFields(typeof(OS)).Where(x => x.FieldType == typeof(Color)))
            {
                OSColorFieldsFast.Add(field.Name, AccessTools.FieldRefAccess<OS, Color>(field));
            }
        }
        
        public ElementInfo ThemeInfo { get; }
        public Texture2D BackgroundImage { get; internal set; }
        private byte[] TextureData = null;
        public string Path { get; }
        public bool Loaded { get; private set; } = false;

        public CachedCustomTheme(string themeFileName)
        {
            Path = themeFileName;
            ElementInfo themeInfo = null;

            var executor = new EventExecutor(themeFileName.ContentFilePath(), true);
            executor.RegisterExecutor("CustomTheme", (exec, info) => themeInfo = info, ParseOption.ParseInterior);
            executor.Parse();

            ThemeInfo = themeInfo ?? throw new FormatException($"No CustomTheme element in {themeFileName}");
        }

        public void Load(bool isMainThread)
        {
            if (ThemeInfo.Children.All(x => x.Name != "backgroundImagePath"))
            {
                Loaded = true;
                return;
            }
            
            if (isMainThread && TextureData == null)
            {
                if (ThemeInfo.Children.TryGetElement("backgroundImagePath", out var imagePath))
                {
                    if (imagePath.Content.HasContent() && imagePath.Content.ContentFileExists())
                    {
                        using (FileStream imageSteam = File.OpenRead(imagePath.Content.ContentFilePath()))
                        {
                            BackgroundImage = Texture2D.FromStream(GuiData.spriteBatch.GraphicsDevice, imageSteam);
                        }

                        Loaded = true;
                    }
                }
            }
            else if (isMainThread)
            {
                using (var ms = new MemoryStream(TextureData))
                {
                    BackgroundImage = Texture2D.FromStream(GuiData.spriteBatch.GraphicsDevice, ms);
                }

                TextureData = null;
                Loaded = true;
            }
            else
            {
                if (ThemeInfo.Children.TryGetElement("backgroundImagePath", out var imagePath))
                {
                    if (imagePath.Content.HasContent() && imagePath.Content.ContentFileExists())
                    {
                        TextureData = File.ReadAllBytes(imagePath.Content.ContentFilePath());
                    }
                }
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(CustomTheme), nameof(CustomTheme.GetThemeForLayout))]
        private static OSTheme GetLayoutForTheme(string name)
        {
            void Manipulator(ILContext il)
            {
                ILCursor c = new ILCursor(il);

                // instead of getting the theme string from this, get it from arg 0
                while (c.TryGotoNext(x =>
                    x.MatchLdfld(AccessTools.Field(typeof(CustomTheme), nameof(CustomTheme.themeLayoutName)))))
                {
                    c.Remove();
                }
            }
            
            Manipulator(null);
            return default;
        }

        public void ApplyTo(OS os)
        {
            if (!Loaded)
                throw new InvalidOperationException("Can't apply a custom theme before it has finished loading!");
            
            ThemeManager.switchTheme(os, OSTheme.HacknetBlue);
                
            foreach (var setting in ThemeInfo.Children)
            {
                if (OSColorFieldsFast.TryGetValue(setting.Name, out var field))
                {
                    ref Color fieldRef = ref field(os);
                    fieldRef = Utils.convertStringToColor(setting.Content);
                }
                else if (setting.Name == "UseAspectPreserveBackgroundScaling")
                {
                    if (bool.TryParse(setting.Content, out var preserve))
                        os.UseAspectPreserveBackgroundScaling = preserve;
                }
                else if (setting.Name == "themeLayoutName")
                {
                    ThemeManager.switchThemeLayout(os, GetLayoutForTheme(setting.Content));
                }
            }

            ThemeManager.backgroundImage = BackgroundImage;
            ThemeManager.LastLoadedCustomThemePath = Path;
            os.RefreshTheme();
        }

        public void Dispose()
        {
            BackgroundImage?.Dispose();
        }
    }
}