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
    // global variables persist between runs of the PB
    partial class Program : MyGridProgram
    {

        // rotors and hinges lock above this speed:
        int maxSafeSpeed = 80;
        bool isSafetyLocked = false;
        Vector3D position = new Vector3D(0, 0, 0);
        public List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
        public List<object> allRotors = new List<object>();
        public List<IMyMotorAdvancedStator> advancedRotors = new List<IMyMotorAdvancedStator>();
        public List<IMyMotorStator> rotors = new List<IMyMotorStator>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            double speed = GetSpeed();

            if (!isSafetyLocked && speed > maxSafeSpeed)
            {
                foreach (var rotor in allRotors)
                {
                    SwitchRotorLock(rotor as IMyMotorStator, true);
                }
                isSafetyLocked = true;
            }
            else if (isSafetyLocked && speed < maxSafeSpeed)
            {
                foreach (var rotor in allRotors)
                {
                    SwitchRotorLock(rotor as IMyMotorStator, false);
                }
                isSafetyLocked = false;
            }

            if (argument == "update")
            {
                GetStators();
            }
        }

        public void Init()
        {
            GetStators();
        }

        public void SwitchRotorLock(IMyMotorStator rotor, bool switchLock)
        {
            rotor.RotorLock = switchLock;
        }

        public void GetStators()
        {
            GridTerminalSystem.GetBlocks(allBlocks);
            for (int i = allBlocks.Count - 1; i >= 0; i--)
            {
                if (!allBlocks[i].IsSameConstructAs(Me))
                {
                    allBlocks.RemoveAt(i);
                    continue;
                }
                if (allBlocks[i] is IMyMotorAdvancedStator)
                {
                    allRotors.Add(allBlocks[i] as IMyMotorStator);
                }
                else if (allBlocks[i] is IMyMotorStator)
                {
                    allRotors.Add(allBlocks[i] as IMyMotorStator);
                }
            }
            return;
        }

        /// <summary>
        /// Gets the current speed the programmable block is traveling
        /// </summary>
        /// <returns>A double representing the PB's current speed in m/s</returns>
        double GetSpeed()
        {
            Vector3D current_position = Me.GetPosition();
            double speed = ((current_position - position) * 6).Length(); // how far the PB has moved since the last run (1/6s ago)
            position = current_position;

            return speed;
        }
    }
}
