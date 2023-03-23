using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public string SyncRotorTag = "#Sync";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource)
        {
            // Affects the speed that the sync rotor uses. Lower if your rotor is
            // overcorrecting but increase to make it slightly more responsive.
            // Default = 300
            float RotorProportionalGain = 300;

            foreach (var rotor in AllRotors)
            {
              if (General.HasTag(SyncRotorTag, rotor.CustomName)){
                SyncedRotors.Add(rotor);
              }
            }

            IMyMotorStator TargetRotor;
            TargetRotor = GridTerminalSystem.GetBlockWithName("TargetRotor") as IMyMotorStator;
            if (TargetRotor == null)
            {
                Echo("TargetRotor Missing [REQUIRED]");
            }
            IMyMotorStator SyncRotor;
            SyncRotor = GridTerminalSystem.GetBlockWithName("SyncRotor") as IMyMotorStator;
            if (SyncRotor == null)
            {
                Echo("SyncRotor Missing [REQUIRED]");
            }
            if ((SyncRotor != null) && (TargetRotor != null))
            {
                float TargetAngle = TargetRotor.Angle;
                float SyncAngle = SyncRotor.Angle;

                float difference = (TargetAngle - SyncAngle);
                difference %= MathHelper.TwoPi; // Make sure that we are less than -360 deg and 360 deg by wrapping it around and getting the remainder

                if (difference > MathHelper.Pi)
                {
                    difference = -MathHelper.TwoPi + difference;
                }
                else if (difference < -MathHelper.Pi)
                {
                    difference = MathHelper.TwoPi + difference;
                }
                float AzStabVelocity = RotorProportionalGain * difference;
                SyncRotor.TargetVelocityRPM = AzStabVelocity;
            }
        }

        List<IMyMotorAdvancedStator> AllRotors = new List<IMyMotorAdvancedStator>();
        List<IMyMotorAdvancedStator> SyncedRotors = new List<IMyMotorAdvancedStator>();

        class General
        {
            public static bool HasTag(string find, params string[] inText)
            {
                foreach (string text in inText)
                {
                    if (ContainsExact(find.ToLower(), text.ToLower()))
                        return true;
                }
                return false;
            }

            public static bool ContainsExact(string match, string text)
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    text,
                    @"(^|\s)" + match + @"(\s|$)"
                );
            }
        }
    }
}
