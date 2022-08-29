using AbilityAnts;
using Dalamud.Data;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data;
using Lumina.Excel;
using Lumina;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace AbilityAntsPlugin
{ 
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class AbilityAntsUI : IDisposable
    {
        private Configuration Configuration;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private int PreAntTimeMs;
        private int ExistingAntTimeMs = 0;

        private List<ClassJob> Jobs;
        private ClassJob SelectedJob = null;
        private Dictionary<uint, List<Action>> JobActions;
        private List<Action> RoleActions;
        private Dictionary<uint, TextureWrap?> LoadedIcons;

        // passing in the image here just for simplicity
        public AbilityAntsUI(Configuration configuration)
        {
            this.Configuration = configuration;

            PreAntTimeMs = Configuration.PreAntTimeMs;

            Jobs = Services.DataManager.GetExcelSheet<ClassJob>()!.Where(j => j.Role > 0 && j.ItemSoulCrystal.Value?.RowId > 0).ToList();
            Jobs.Sort((lhs, rhs) => lhs.Name.RawString.CompareTo(rhs.Name.RawString));
            JobActions = new();
            foreach(var job in Jobs)
            {
                JobActions[job.RowId] = Services.DataManager.GetExcelSheet<Action>()!.
                    Where(a => !a.IsPvP && a.ClassJob.Value?.ExpArrayIndex == job.ExpArrayIndex && a.IsPlayerAction && (a.ActionCategory.Row == 4 || a.Recast100ms > 100)).ToList();
                JobActions[job.RowId].Sort((lhs, rhs) => lhs.Name.RawString.CompareTo(rhs.Name.RawString));
            }
            RoleActions = Services.DataManager.GetExcelSheet<Action>()!.Where(a => a.IsRoleAction && a.ClassJobLevel != 0).ToList();
            RoleActions.Sort((lhs, rhs) => lhs.Name.RawString.CompareTo(rhs.Name.RawString));

            CacheIcons();
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Ability Ants Config", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                bool showCombat = Configuration.ShowOnlyInCombat;
                if (ImGui.Checkbox("Only show custom ants while in combat", ref showCombat))
                {
                    Configuration.ShowOnlyInCombat = showCombat;
                    Configuration.Save();
                }
                
                bool lastCharge = Configuration.AntOnFinalStack;
                if (ImGui.Checkbox("Charged abilities only get ants for the final charge", ref lastCharge))
                {
                    Configuration.AntOnFinalStack = lastCharge;
                }

                ImGui.SetNextItemWidth(75);
                if (ImGui.InputInt("##default", ref PreAntTimeMs, 0))
                {
                    // This section intentionally left blank
                }
                ImGui.SameLine();
                if (ImGui.Button("Set default pre-ant time, in ms"))
                {
                    Configuration.PreAntTimeMs = PreAntTimeMs;
                }

                ImGui.SetNextItemWidth(75);
                if (ImGui.InputInt("##existing", ref ExistingAntTimeMs, 0))
                {
                    // This section intentionally left blank
                }
                ImGui.SameLine();
                if (ImGui.Button("Set saved pre-ant times to this, in ms"))
                {
                    foreach (var (k, _) in Configuration.ActiveActions)
                        Configuration.ActiveActions[k] = ExistingAntTimeMs;
                }


                if (ImGui.BeginChild("sidebar", new(ImGui.GetContentRegionAvail().X * (float)0.25, ImGui.GetContentRegionAvail().Y)))
                {
                    if (ImGui.Selectable("Role Actions", SelectedJob == null))
                    {
                        SelectedJob = null;
                    }
                    foreach (var job in Jobs)
                    {
                        if (ImGui.Selectable(job.Abbreviation))
                        {
                            SelectedJob = job;
                        }
                    }
                    ImGui.EndChild();
                }

                ImGui.SameLine();

                if (ImGui.BeginChild("testo2", new(-1, -1)))
                {
                    List<Action> actions;
                    if (SelectedJob != null)
                    {
                        ImGui.PushID(SelectedJob.Abbreviation.RawString);
                        actions = JobActions[SelectedJob.RowId];
                    }
                    else
                    {
                        ImGui.PushID("job actions");
                        actions = RoleActions;
                    }
                    DrawActions(actions);
                    ImGui.PopID();
                    ImGui.EndChild();
                }

                ImGui.Spacing();

                ImGui.Text("Have a goat:");
                ImGui.Indent(55);
                ImGui.Unindent(55);
            }
            ImGui.End();
            Configuration.Save();
        }
        void DrawActions(List<Action> actions)
        {
            foreach (var action in actions)
            {
                ImGui.PushID(action.Name.RawString);
                bool active = Configuration.ActiveActions.ContainsKey(action.RowId);
                int preTime;
                DrawIcon(action);
                ImGui.SameLine();
                ImGui.Text(action.Name.RawString);
                ImGui.SameLine();
                if (ImGui.Checkbox("Active", ref active))
                {
                    if (active)
                    {
                        Configuration.ActiveActions.Add(action.RowId, Configuration.PreAntTimeMs);
                        preTime = Configuration.PreAntTimeMs;
                    }
                    else
                    {
                        Configuration.ActiveActions.Remove(action.RowId);
                    }
                }
                if (Configuration.ActiveActions.ContainsKey(action.RowId))
                {
                    ImGui.SameLine();
                    int preAntTime = Configuration.ActiveActions[action.RowId];
                    ImGui.SetNextItemWidth(75);
                    if (ImGui.InputInt("ms pre-ant", ref preAntTime, 0))
                    {
                        Configuration.ActiveActions[action.RowId] = preAntTime;
                    }
                }
                ImGui.PopID();
            }
        }

        void DrawIcon(Action action)
        {
            var texture = LoadedIcons[action.RowId];
            ImGui.Image(texture.ImGuiHandle, new(texture.Width, texture.Height));
        }

        void CacheIcons()
        {
            LoadedIcons = new();
            foreach (var action in RoleActions)
            {
                TextureWrap? tw = Services.DataManager.GetImGuiTextureIcon(action.Icon);
                LoadedIcons[action.RowId] = tw;
            }
            foreach (var (_, v) in JobActions)
            {
                foreach (var action in v)
                {
                    TextureWrap? tw = Services.DataManager.GetImGuiTextureIcon(action.Icon);
                    LoadedIcons[action.RowId] = tw;
                }
            }
        }
    }

}
