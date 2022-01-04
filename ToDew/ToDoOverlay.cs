﻿// Copyright 2021 Jamie Taylor
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;

namespace ToDew {
    public class OverlayConfig {
        public bool enabled = true;
        public SButton hotkey = SButton.None;
        public KeybindList hotkeyList = new KeybindList();
        public bool hideAtFestivals = false;
        public int maxWidth = 600;
        public int maxItems = 10;
        public Color backgroundColor = Color.Black * 0.2f;
        public Color textColor = Color.White * 0.8f;
        public int offsetX = 0;
        public int offsetY = 0;
        public static void RegisterConfigMenuOptions(Func<OverlayConfig> getThis, GenericModConfigMenuAPI api, GMCMOptionsAPI apiExt, IManifest modManifest) {
            api.AddSectionTitle(modManifest, I18n.Config_Overlay, I18n.Config_Overlay_Desc);
            api.AddBoolOption(
                mod: modManifest,
                name: I18n.Config_Overlay_Enabled,
                tooltip: I18n.Config_Overlay_Enabled_Desc,
                getValue: () => getThis().enabled,
                setValue: (bool val) => getThis().enabled = val);
            api.AddKeybind(
                mod: modManifest,
                name: I18n.Config_Overlay_Hotkey,
                tooltip: I18n.Config_Overlay_Hotkey_Desc,
                getValue: () => getThis().hotkey,
                setValue: (SButton val) => getThis().hotkey = val);
            api.AddBoolOption(
                mod: modManifest,
                name: I18n.Config_Overlay_HideAtFestivals,
                tooltip: I18n.Config_Overlay_HideAtFestivals_Desc,
                getValue: () => getThis().hideAtFestivals,
                setValue: (bool val) => getThis().hideAtFestivals = val);
            api.AddNumberOption(
                mod: modManifest,
                name: I18n.Config_Overlay_MaxWidth,
                tooltip: I18n.Config_Overlay_MaxWidth_Desc,
                getValue: () => getThis().maxWidth,
                setValue: (int val) => getThis().maxWidth = val);
            api.AddNumberOption(
                mod: modManifest,
                name: I18n.Config_Overlay_MaxItems,
                tooltip: I18n.Config_Overlay_MaxItems_Desc,
                getValue: () => getThis().maxItems,
                setValue: (int val) => getThis().maxItems = val);
            if (apiExt is not null) {
                apiExt.AddColorOption(
                    mod: modManifest,
                    name: I18n.Config_Overlay_BackgroundColor,
                    tooltip: I18n.Config_Overlay_BackgroundColor_Desc,
                    getValue: () => getThis().backgroundColor,
                    setValue: (c) => getThis().backgroundColor = c,
                    colorPickerStyle: (uint)(GMCMOptionsAPI.ColorPickerStyle.AllStyles | GMCMOptionsAPI.ColorPickerStyle.RadioChooser));
                apiExt.AddColorOption(
                    mod: modManifest,
                    name: I18n.Config_Overlay_TextColor,
                    tooltip: I18n.Config_Overlay_TextColor_Desc,
                    getValue: () => getThis().textColor,
                    setValue: (c) => getThis().textColor = c,
                    colorPickerStyle: (uint)(GMCMOptionsAPI.ColorPickerStyle.AllStyles | GMCMOptionsAPI.ColorPickerStyle.RadioChooser));
            }
        }
    }
    public class ToDoOverlay : IDisposable {
        private readonly ModEntry theMod;
        private readonly ToDoList theList;
        private OverlayConfig config { get => theMod.config.overlay; }
        private string ListHeader = I18n.Overlay_Header();
        private const int marginTop = 5;
        private const int marginLeft = 5;
        private const int marginRight = 5;
        private const int marginBottom = 5;
        private const int lineSpacing = 5;
        private readonly SpriteFont font = Game1.smallFont;
        private readonly Vector2 ListHeaderSize;
        private List<String> lines;
        private List<float> lineHeights;
        private List<bool> lineBold;
        private Rectangle bounds;
        public ToDoOverlay(ModEntry theMod, ToDoList theList) {
            this.theMod = theMod;
            this.theList = theList;
            // save "constant" values
            ListHeaderSize = font.MeasureString(ListHeader);
            // initialize rendering callback
            theMod.Helper.Events.Display.RenderedWorld += OnRenderedWorld;
            // initialize the list UI and callback
            theList.OnChanged += OnListChanged;
            syncMenuItemList();
        }

        private void syncMenuItemList() {
            lines = new List<string>();
            lineHeights = new List<float>();
            lineBold = new List<bool>();
            if (theList.Items.Count == 0) return;
            float availableWidth = Math.Max(config.maxWidth - marginLeft - marginRight, ListHeaderSize.X);
            float usedWidth = ListHeaderSize.X;
            float topPx = marginTop + ListHeaderSize.Y;
            foreach (var item in theList.Items) {
                if (item.IsDone || item.HideInOverlay || ! item.IsVisibleToday) continue;
                if (lines.Count >= config.maxItems) {
                    lines.Add("…");
                    float lineHeight = font.MeasureString("…").Y;
                    lineHeights.Add(lineHeight);
                    lineBold.Add(false);
                    topPx += lineHeight;
                    break;
                }
                topPx += lineSpacing;
                string itemText = item.IsHeader ? item.Text : ("  " + item.Text);
                var lineSize = font.MeasureString(itemText);
                while (lineSize.X > availableWidth) {
                    if (itemText.Length < 2) {
                        // this really shouldn't happen
                        break;
                    }
                    itemText = itemText.Remove(itemText.Length - 2) + "…";
                    lineSize = font.MeasureString(itemText);
                }
                usedWidth = Math.Max(usedWidth, lineSize.X);
                lines.Add(itemText);
                lineHeights.Add(lineSize.Y);
                lineBold.Add(item.IsBold);
                topPx += lineSize.Y;
            }
            bounds = new Rectangle(config.offsetX, config.offsetY, (int)(usedWidth + marginLeft + marginRight), (int)topPx + marginBottom);
        }
        private void OnListChanged(object sender, List<ToDoList.ListItem> e) {
            syncMenuItemList();
        }

        public void Dispose() {
            this.theList.OnChanged -= OnListChanged;
            theMod.Helper.Events.Display.RenderedWorld -= OnRenderedWorld;
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e) {
            if (lines.Count == 0) return;
            if (!config.enabled) return; // shouldn't get this far, but why not check anyway
            if (Game1.game1.takingMapScreenshot) return;
            if (Game1.eventUp || Game1.farmEvent != null) return;
            if (config.hideAtFestivals && Game1.isFestival()) return;
            var spriteBatch = e.SpriteBatch;
            Rectangle effectiveBounds = bounds;
            if (Game1.CurrentMineLevel > 0 || Game1.currentLocation is VolcanoDungeon vd && vd.level.Value > 0) {
                effectiveBounds.Y += 80;
            }
            float topPx = effectiveBounds.Y + marginTop;
            float leftPx = effectiveBounds.X + marginLeft;
            spriteBatch.Draw(Game1.fadeToBlackRect, effectiveBounds, config.backgroundColor);
            Utility.drawBoldText(spriteBatch, ListHeader, font, new Vector2(leftPx, topPx), config.textColor);
            topPx += ListHeaderSize.Y;
            spriteBatch.DrawLine(leftPx, topPx, new Vector2(ListHeaderSize.X - 3, 1), config.textColor);
            for (int i = 0; i < lines.Count; i++) {
                topPx += lineSpacing;
                if (lineBold[i]) {
                    Utility.drawBoldText(spriteBatch, lines[i], font, new Vector2(leftPx, topPx), config.textColor);
                } else {
                    spriteBatch.DrawString(font, lines[i], new Vector2(leftPx, topPx), config.textColor);
                }
                topPx += lineHeights[i];
            }
        }

    }
}
