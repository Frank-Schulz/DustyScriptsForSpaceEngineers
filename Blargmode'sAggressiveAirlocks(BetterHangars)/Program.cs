using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /*
        ------------------------------------------------
        Edit of Blargmode's Aggressive Airlocks
        Edited from Version: 6.4 (2020-08-27)

        Made the #Hangar tag treat any door like a hangar door, the original only 
        treated hanger doors this way. Added check on line 1073

        Original description follows:
        ------------------------------------------------

        Probably the best airlock script on the workshop ;)

        ___/ Installation \\_________
        If you're reading this you have probably installed it already.
        Press 'Ok' and you're done.


        ___/ The update command \\_________
        Important for later. Sending the update command tells the script that
        you have made changes it should know about.
        To do it, type 'update' in the 'Argument' text field in the programmable
        block, and press Run. This can also be done via for example a button.


        ___/ Tags \\_________
        Tags are used to tell the script about what blocks are what.
        You enter them into the 'Custom Name' field of the block.
        It does not matter if it's first or last or somewhere in between.

        Currently there are 4 tags, default values below:
        #AL     - Marks the outer doors in Smart and Group-airlocks.
        #Hangar - Marks the outer doors in Hangars.
        #Ignore - Marks any block that the script should, well, ignore.
                  Useful for example if the Smart airlock isn't smart enough.
        #Manual - Disables auto-closing.


        ___/ Setup - Regular doors \\_________
        Just send the update command to the script. It will find all new doors.


        ___/ Setup - Tiny airlocks \\_________
        Place two doors touching in a line and send the update command.


        ___/ Setup - Smart airlocks \\_________
        Build 2 doors and 1 air vent in close proximity, and tag the outer door with #AL.
        Then send the update command. The script will start at the outer door and look for the
        closest air vent, as well as the closest untagged door.
        These three becomes the airlock.


        ___/ Setup - Group airlocks \\_________
        Build 2 or more doors and 1 or more air vents.
        You can also include lights, LCDs, Oxygen Tanks, O2/H2 Generators, and Oxygen farms.
        Tag the outside doors (all of them) with #AL and put everything into a group.
        The group name does not matter. Then send the update command.
        The script will create one airlock from every valid group.
        Included Oxygen tanks, farms, and generators will be automatically managed.


        ___/ Setup - Simple Group airlocks \\_________
        Build 2 or more doors and group them. Name doesn't matter.
        Tag 1+ doors with #AL, then send the update command.
        The resulting airlock has no oxygen capabilities, but prevents tagged and
        untagged doors from being open simultaneously.
        If you tag all doors in the group; it goes into solo mode: Only one door can be opened at a time.


        ___/ Setup - Hangars \\_________
        Exactly the same as Group airlocks, but you use the #Hangar tag instead.
        The inner door isn't required.
        You will need buttons both inside and outside of the hangar, as that is how you toggle it.
        Set up one of the air vents "Depressurize On/Off" action in the buttons and you're done.


        ___/ Additional stuff \\_________
        Atmosphere mode: All the airlocks with Air vents can detect atmosphere
        when you head inside. Atmosphere mode stops depressurization, preventing
        oxygen tanks from overfilling.

        Naming the group airlock and hangar:
        These airlocks can have LCDs, which will display a name like "Hangar
        01". If you edit the Title of an LCD in the group and send the update
        command, all LCDs in the group will show your new name.

        Settings:
        There are a lot of settings in custom data of the programmable block.
        Send the update command for any changes to take effect.

        Safety features:
        If an airlock detects it's stuck (de)pressurizing, it
        will abort and open the door.

        Otherwise you'd be stuck forever.

        Hydrogen production:
        Hydrogen production interferes with the airlocks as
        it tends to fill upp oxygen tanks, preventing depressurization. I
        recommend having separate conveyor systems for your air system and
        hydrogen system.



        Compressed code, touchy = breaky ¯\_(˘·˘)_/¯
        */


        class AdvancedAirlock : AirManagedAirlock
        {
            bool innerOpenRequest = false;
            bool outerOpenRequest = false;

            public AdvancedAirlock(AirlockComponents components) : base(components)
            {
                foreach (var door in components.outer)
                {
                    door.SubscribeFunc(OuterDoorAction);
                }
                foreach (var door in components.inner)
                {
                    door.SubscribeFunc(InnerDoorAction);
                }
            }

            public override void Update()
            {
                base.Update();
                if (AirlockState == AirlockState2.AwatingTotalLock)
                {
                    if (innerOpenRequest)
                    {
                        if (OuterOpenCount <= 0)
                        {
                            if (components.P.inAtmo)
                            {
                                EnableVents(false);
                            }
                            else
                            {
                                Depressurize(false);
                            }
                            timeout = components.P.Time + ventDeadline;
                            startOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                            AirlockState = AirlockState2.Pressurizing;
                        }
                    }
                    if (outerOpenRequest)
                    {
                        if (InnerOpenCount <= 0)
                        {
                            if (components.P.inAtmo && !attemptAirScoop)
                            {
                                EnableVents(false);
                            }
                            else
                            {
                                Depressurize(true);
                            }
                            timeout = components.P.Time + ventDeadline;
                            startOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                            AirlockState = AirlockState2.Depressurizing;
                        }
                    }
                }
                if (AirlockState == AirlockState2.Pressurizing)
                {
                    if (innerOpenRequest)
                    {
                        if (startOxygenLevel > 0.8)
                        {
                            maybeAtmoSkipDepressurization = true;
                        }
                        else
                        {
                            maybeAtmoSkipDepressurization = false;
                        }
                        currentOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                        if (components.P.Time > timeout)
                        {
                            if (
                                Math.Abs(currentOxygenLevel - startOxygenLevel)
                                < components.oxygenDifferenceRequired
                            )
                            {
                                currentOxygenLevel = 1;
                                timeout = TimeSpan.MaxValue;
                            }
                            else
                            {
                                startOxygenLevel = currentOxygenLevel;
                                timeout = components.P.Time + ventDeadline;
                            }
                        }
                        if (currentOxygenLevel > 0.9 || components.P.inAtmo)
                        {
                            innerOpenRequest = false;
                            EnableDoors(components.inner, true);
                            OpenAll(components.inner);
                            if (components.P.Time > timeout)
                            {
                                errorStatus = "Pressurization failed";
                            }
                            else
                            {
                                errorStatus = "";
                            }
                            timeout = TimeSpan.MaxValue;
                            AirlockState = AirlockState2.InnerOpen;
                        }
                    }
                    StatusService(AirlockState);
                }
                if (AirlockState == AirlockState2.Depressurizing)
                {
                    currentOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                    if (components.P.Time > timeout)
                    {
                        if (
                            Math.Abs(currentOxygenLevel - startOxygenLevel)
                            < components.oxygenDifferenceRequired
                        )
                        {
                            currentOxygenLevel = 0;
                            timeout = TimeSpan.MaxValue;
                        }
                        else
                        {
                            startOxygenLevel = currentOxygenLevel;
                            timeout = components.P.Time + ventDeadline;
                        }
                    }
                    if (
                        currentOxygenLevel < 0.1
                        || tanksFullSkipDepressurization
                        || components.P.inAtmo
                        || maybeAtmoSkipDepressurization
                    )
                    {
                        if (attemptAirScoop == false)
                        {
                            EnableVents(false);
                        }
                        if (outerOpenRequest)
                        {
                            outerOpenRequest = false;
                            EnableDoors(components.outer, true);
                            OpenAll(components.outer);
                            AirlockState = AirlockState2.OuterOpen;
                        }
                        else
                        {
                            EnableDoors(components.outer, true);
                            EnableDoors(components.inner, true);
                            AirlockState = AirlockState2.Neutral;
                        }
                        if (components.P.Time > timeout)
                        {
                            errorStatus = "Depressurization failed";
                        }
                        else
                        {
                            errorStatus = "";
                        }
                        timeout = TimeSpan.MaxValue;
                    }
                    StatusService(AirlockState);
                }
                if (AirlockState == AirlockState2.Unknown)
                {
                    EnableDoors(components.outer, true);
                    EnableDoors(components.inner, true);
                    if (InnerOpenCount <= 0 && OuterOpenCount <= 0)
                    {
                        Depressurize(true);
                        AirlockState = AirlockState2.Neutral;
                    }
                }
            }

            public override void OuterDoorAction(ExtendedDoor door)
            {
                base.OuterDoorAction(door);
                if (AirlockState == AirlockState2.Neutral)
                {
                    if (door.door.Status == DoorStatus.Opening && !door.ProgramOpening)
                    {
                        innerOpenRequest = true;
                        SendLockRequest(components.inner);
                        SendLockRequest(components.outer);
                        AirlockState = AirlockState2.AwatingTotalLock;
                    }
                }
                if (AirlockState == AirlockState2.OuterOpen)
                {
                    if (OuterOpenCount <= 0)
                    {
                        EnableDoors(components.inner, true);
                        AirlockState = AirlockState2.Neutral;
                    }
                }
            }

            public override void InnerDoorAction(ExtendedDoor door)
            {
                base.InnerDoorAction(door);
                if (AirlockState == AirlockState2.Neutral)
                {
                    if (door.door.Status == DoorStatus.Opening && !door.ProgramOpening)
                    {
                        outerOpenRequest = true;
                        Depressurize(false);
                        SendLockRequest(components.inner);
                        SendLockRequest(components.outer);
                        AirlockState = AirlockState2.AwatingTotalLock;
                    }
                }
                if (AirlockState == AirlockState2.InnerOpen)
                {
                    if (InnerOpenCount <= 0)
                    {
                        Depressurize(true);
                        timeout = components.P.Time + ventDeadline;
                        startOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                        AirlockState = AirlockState2.Depressurizing;
                    }
                }
            }

            public override void StatusService(AirlockState2 state)
            {
                switch (state)
                {
                    case AirlockState2.Neutral:
                        components.SetLightsIdle();
                        break;
                    case AirlockState2.AwatingTotalLock:
                        components.SetLightsWorking();
                        if (innerOpenRequest)
                        {
                            components.TriggerInnerTimers();
                        }
                        if (outerOpenRequest)
                        {
                            components.TriggerOuterTimers();
                        }
                        break;
                    case AirlockState2.Unknown:
                        components.SetLightsWorking();
                        break;
                }
                if (components.statusDisplay == null)
                    return;
                switch (state)
                {
                    case AirlockState2.Neutral:
                        if (errorStatus.Length > 0)
                        {
                            components.statusDisplay.Update(errorStatus, true);
                        }
                        else
                        {
                            components.statusDisplay.Update("Ready");
                        }
                        break;
                    case AirlockState2.AwatingTotalLock:
                        components.statusDisplay.Update("Locking doors");
                        break;
                    case AirlockState2.Depressurizing:
                        components.statusDisplay.Update("Depressurizing");
                        break;
                    case AirlockState2.Pressurizing:
                        components.statusDisplay.Update("Pressurizing");
                        break;
                    case AirlockState2.OuterOpen:
                        if (errorStatus.Length > 0)
                        {
                            components.statusDisplay.Update(errorStatus, true);
                        }
                        else
                        {
                            components.statusDisplay.Update("Outer open");
                        }
                        break;
                    case AirlockState2.InnerOpen:
                        if (errorStatus.Length > 0)
                        {
                            components.statusDisplay.Update(errorStatus, true);
                        }
                        else
                        {
                            components.statusDisplay.Update("Inner open");
                        }
                        break;
                    case AirlockState2.Unknown:
                        components.statusDisplay.Update("Setup in progress");
                        break;
                    default:
                        components.statusDisplay.Update("No idea what's going on");
                        break;
                }
            }
        }

        class AirlockComponents
        {
            public Program P;
            public List<ExtendedDoor> inner;
            public List<ExtendedDoor> outer;
            public float secondsBeforeTimeout = 2;
            public float oxygenDifferenceRequired = .2f;
            public List<IMyAirVent> vents;
            public List<ExtendedAirvent> extendedVents;
            public AirlockStatusDisplay statusDisplay = null;
            public List<IMyLightingBlock> lights = null;
            public List<IMyGasTank> tanks = null;
            public List<IMyGasGenerator> generators = null;
            public List<IMyFunctionalBlock> farms = null;
            public List<IMyTimerBlock> outerTimers = null;
            public List<IMyTimerBlock> innerTimers = null;
            Color neutral;
            Color working;

            public AirlockComponents(
                Program p,
                List<ExtendedDoor> outer,
                List<ExtendedDoor> inner,
                List<IMyAirVent> vents
            )
            {
                P = p;
                this.outer = outer;
                this.inner = inner;
                this.vents = vents;
                secondsBeforeTimeout = (float)P.settings[ID.Timeout].Value;
                neutral = (Color)P.settings[ID.DefaultLampColor].Value;
                working = (Color)P.settings[ID.ChangingLampColor].Value;
                float temp = (float)P.settings[ID.OxygenDifference].Value / 100;
                if (temp <= 1 && temp >= 0)
                {
                    oxygenDifferenceRequired = temp;
                }
            }

            public AirlockComponents(
                Program p,
                List<ExtendedDoor> outer,
                List<ExtendedDoor> inner,
                List<ExtendedAirvent> extendedVents
            )
            {
                P = p;
                this.outer = outer;
                this.inner = inner;
                this.extendedVents = extendedVents;
                secondsBeforeTimeout = (float)P.settings[ID.Timeout].Value;
                neutral = (Color)P.settings[ID.DefaultLampColor].Value;
                working = (Color)P.settings[ID.ChangingLampColor].Value;
                float temp = (float)P.settings[ID.OxygenDifference].Value / 100;
                if (temp <= 1 && temp >= 0)
                {
                    oxygenDifferenceRequired = temp;
                }
            }

            public AirlockComponents(
                Program p,
                ExtendedDoor outer,
                ExtendedDoor inner,
                ExtendedAirvent extendedVents
            )
            {
                P = p;
                this.outer = new List<ExtendedDoor> { outer };
                this.inner = new List<ExtendedDoor> { inner };
                this.extendedVents = new List<ExtendedAirvent> { extendedVents };
                secondsBeforeTimeout = (float)P.settings[ID.Timeout].Value;
                neutral = (Color)P.settings[ID.DefaultLampColor].Value;
                working = (Color)P.settings[ID.ChangingLampColor].Value;
                float temp = (float)P.settings[ID.OxygenDifference].Value / 100;
                if (temp <= 1 && temp >= 0)
                {
                    oxygenDifferenceRequired = temp;
                }
            }

            public void TriggerOuterTimers()
            {
                if (outerTimers != null)
                {
                    for (int i = 0; i < outerTimers.Count; i++)
                    {
                        outerTimers[i].Trigger();
                    }
                }
            }

            public void TriggerInnerTimers()
            {
                if (innerTimers != null)
                {
                    for (int i = 0; i < innerTimers.Count; i++)
                    {
                        innerTimers[i].Trigger();
                    }
                }
            }

            public void SetLightsWorking()
            {
                if (lights != null)
                {
                    for (int i = 0; i < lights.Count; i++)
                    {
                        lights[i].Color = working;
                        lights[i].BlinkIntervalSeconds = 1.2f;
                        lights[i].BlinkLength = 40f;
                    }
                }
            }

            public void SetLightsIdle()
            {
                if (lights != null)
                {
                    for (int i = 0; i < lights.Count; i++)
                    {
                        lights[i].Color = neutral;
                        lights[i].BlinkIntervalSeconds = 0;
                    }
                }
            }
        }

        enum PanelType
        {
            Corner,
            Text,
            Wide,
            Normal
        }

        class AirlockStatusDisplay
        {
            Program P;
            IMyTextPanel[] panels;
            PanelType[] types;
            public string airlockName = "";
            private string airlockType;

            public AirlockStatusDisplay(Program p, IMyTextPanel panel, string airlockType)
            {
                P = p;
                panels = new IMyTextPanel[] { panel };
                this.airlockType = airlockType;
                GetPanelType();
            }

            public AirlockStatusDisplay(Program p, List<IMyTextPanel> panels, string airlockType)
            {
                P = p;
                this.panels = panels.ToArray();
                this.airlockType = airlockType;
                GetPanelType();
            }

            public void GetPanelType()
            {
                types = new PanelType[panels.Length];
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i].GetPublicTitle().Length > 0)
                    {
                        airlockName = panels[i].GetPublicTitle();
                    }
                    if (panels[i].BlockDefinition.SubtypeId.Contains("Corner"))
                    {
                        if (panels[i].BlockDefinition.SubtypeId.Contains("Flat"))
                        {
                            if (panels[i].FontSize == 1f)
                            {
                                panels[i].FontSize = 1.4f;
                            }
                        }
                        else
                        {
                            if (panels[i].FontSize == 1f)
                            {
                                panels[i].FontSize = 1.3f;
                            }
                        }
                        types[i] = PanelType.Corner;
                    }
                    else
                    {
                        types[i] = PanelType.Normal;
                    }
                }
            }

            public void Update(string ventState, bool error = false)
            {
                string text = "";
                for (int i = 0; i < panels.Length; i++)
                {
                    if (text.Length == 0)
                    {
                        if (error)
                        {
                            text += " <<< " + P.strings[Str.Error] + " >>>";
                        }
                        if (airlockName.Length < 1)
                        {
                            text += " " + airlockType + " \n";
                        }
                        else
                        {
                            text += " " + airlockName + " \n";
                        }
                        text += " " + ventState + " ";
                    }
                    panels[i].ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                    panels[i].WriteText(text);
                }
            }
        }

        enum AirlockState2
        {
            Neutral,
            InnerOpen,
            OuterOpen,
            Pressurizing,
            Depressurizing,
            AwatingInnerLock,
            AwatingOuterLock,
            AwatingTotalLock,
            Unknown
        }

        abstract class AirManagedAirlock
        {
            public AirlockComponents components;
            public List<ExtendedDoor> outerLockRequest = new List<ExtendedDoor>();
            public List<ExtendedDoor> innerLockRequest = new List<ExtendedDoor>();
            public bool outerChange = false;
            public bool innerChange = false;
            public bool ventChange = false;
            public TimeSpan timeout = TimeSpan.MaxValue;
            public TimeSpan ventDeadline;
            public double startOxygenLevel = 0;
            public double currentOxygenLevel = 0;
            public bool maybeAtmoSkipDepressurization = false;
            bool generatorsEnabled = false;
            public bool GeneratorsEnabled
            {
                get { return generatorsEnabled; }
                set
                {
                    generatorsEnabled = value;
                    if (components.generators != null)
                    {
                        for (int i = 0; i < components.generators.Count; i++)
                        {
                            components.generators[i].Enabled = value;
                        }
                    }
                    if (components.farms != null)
                    {
                        for (int i = 0; i < components.farms.Count; i++)
                        {
                            components.farms[i].Enabled = value;
                        }
                    }
                }
            }
            public bool attemptAirScoop = false;
            public bool tanksFullSkipDepressurization = false;
            AirlockState2 airlockState = AirlockState2.Unknown;
            public AirlockState2 AirlockState
            {
                get { return airlockState; }
                set
                {
                    airlockState = value;
                    StatusService(value);
                }
            }
            int outerOpenCount = 0;
            public int OuterOpenCount
            {
                get { return outerOpenCount; }
                set
                {
                    outerOpenCount = value;
                    if (outerOpenCount < 0)
                    {
                        CalcOpenCount();
                    }
                }
            }
            int innerOpenCount = 0;
            public int InnerOpenCount
            {
                get { return innerOpenCount; }
                set
                {
                    innerOpenCount = value;
                    if (innerOpenCount < 0)
                    {
                        CalcOpenCount();
                    }
                }
            }
            public string errorStatus = "";

            public AirManagedAirlock(AirlockComponents components)
            {
                this.components = components;
                ventDeadline = TimeSpan.FromSeconds(components.secondsBeforeTimeout);
                GeneratorsEnabled = false;
                CalcOpenCount();
            }

            public virtual void Update()
            {
                outerChange = false;
                innerChange = false;
                ventChange = false;
                for (int i = 0; i < components.outer.Count; i++)
                {
                    components.outer[i].Update();
                }
                for (int i = 0; i < components.inner.Count; i++)
                {
                    components.inner[i].Update();
                }
                for (int i = 0; i < components.extendedVents.Count; i++)
                {
                    components.extendedVents[i].Update();
                }
                if (components.tanks != null)
                {
                    double totalFillRatio = 0;
                    for (int i = 0; i < components.tanks.Count; i++)
                    {
                        totalFillRatio = components.tanks[i].FilledRatio;
                    }
                    totalFillRatio = totalFillRatio / components.tanks.Count;
                    if (totalFillRatio > .95)
                    {
                        tanksFullSkipDepressurization = true;
                    }
                    else
                    {
                        tanksFullSkipDepressurization = false;
                    }
                    if (totalFillRatio > .7)
                    {
                        attemptAirScoop = false;
                    }
                    else if (totalFillRatio < .65)
                    {
                        attemptAirScoop = true;
                    }
                    if (GeneratorsEnabled == true && totalFillRatio > .7)
                    {
                        GeneratorsEnabled = false;
                    }
                    else if (GeneratorsEnabled == false && totalFillRatio < .3)
                    {
                        GeneratorsEnabled = true;
                    }
                }
            }

            public virtual void OuterDoorAction(ExtendedDoor door)
            {
                if (door.door.Status == DoorStatus.Opening)
                {
                    OuterOpenCount++;
                }
                else if (door.door.Status == DoorStatus.Closed)
                {
                    OuterOpenCount--;
                }
            }

            public virtual void InnerDoorAction(ExtendedDoor door)
            {
                if (door.door.Status == DoorStatus.Opening)
                {
                    InnerOpenCount++;
                }
                else if (door.door.Status == DoorStatus.Closed)
                {
                    InnerOpenCount--;
                }
            }

            public void RequestLockOuter()
            {
                outerLockRequest.AddList(components.outer);
            }

            public void RequestLockInner()
            {
                innerLockRequest.AddList(components.inner);
            }

            public void EnableDoors(List<ExtendedDoor> doors, bool enabled)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    doors[i].door.Enabled = enabled;
                }
            }

            public void SendLockRequest(List<ExtendedDoor> doors, bool force = false)
            {
                if (!force && components.P.inAtmo)
                    return;
                for (int i = 0; i < doors.Count; i++)
                {
                    doors[i].lockRequest = true;
                }
            }

            public void Depressurize(bool depressurize)
            {
                for (int i = 0; i < components.extendedVents.Count; i++)
                {
                    components.extendedVents[i].Enabled = true;
                    components.extendedVents[i].Depressurize = depressurize;
                }
            }

            public void EnableVents(bool enable)
            {
                for (int i = 0; i < components.extendedVents.Count; i++)
                {
                    components.extendedVents[i].Enabled = enable;
                }
            }

            public void OpenAll(List<ExtendedDoor> doors)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    if (!doors[i].isManualDoor)
                    {
                        doors[i].ProgramOpen();
                    }
                }
            }

            public void CloseAll(List<ExtendedDoor> doors, bool onlyManualDoors = false)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    if (!onlyManualDoors || doors[i].isManualDoor || doors[i].isHangarDoor)
                    {
                        doors[i].door.CloseDoor();
                    }
                }
            }

            public void CalcOpenCount()
            {
                outerOpenCount = 0;
                for (int i = 0; i < components.outer.Count; i++)
                {
                    if (components.outer[i].door.Status != DoorStatus.Closed)
                    {
                        outerOpenCount++;
                    }
                }
                innerOpenCount = 0;
                for (int i = 0; i < components.inner.Count; i++)
                {
                    if (components.inner[i].door.Status != DoorStatus.Closed)
                    {
                        innerOpenCount++;
                    }
                }
            }

            public abstract void StatusService(AirlockState2 state);
        }

        public class ExecutionTime
        {
            Program P;
            const int Size = 10;
            int[] Count;
            int Index;
            DateTime StartTime;
            double[] Times;

            public ExecutionTime(Program p)
            {
                P = p;
                Count = new int[Size];
                Times = new double[Size];
            }

            public void Update()
            {
                if (Index >= Size)
                    Index = 0;
                Count[Index] = P.Runtime.CurrentInstructionCount;
                Times[Index] = P.Runtime.LastRunTimeMs;
                Index++;
            }

            public void Start()
            {
                StartTime = DateTime.Now;
            }

            public void End()
            {
                if (Index >= Size)
                    Index = 0;
                Count[Index] = P.Runtime.CurrentInstructionCount;
                Times[Index] = (DateTime.Now - StartTime).TotalMilliseconds;
                Index++;
            }

            public double GetAvrage()
            {
                return Count.Average();
            }

            public int GetPeak()
            {
                return Count.Max();
            }

            public double GetAvrageTime()
            {
                return Times.Average();
            }

            public double GetPeakTime()
            {
                return Times.Max();
            }
        }

        class ExtendedAirvent
        {
            Program P;
            private IMyAirVent vent;
            private bool lastVentState;
            public bool Depressurize
            {
                get { return vent.Depressurize; }
                set
                {
                    lastVentState = value;
                    vent.Depressurize = value;
                }
            }
            public bool CanPressurize
            {
                get { return vent.CanPressurize; }
            }
            public bool Enabled
            {
                get { return vent.Enabled; }
                set { vent.Enabled = value; }
            }
            public string CustomName
            {
                get { return vent.CustomName; }
                set { vent.CustomName = value; }
            }
            public bool ChangedThisUpdate { get; private set; }
            List<Action> EventActions = new List<Action>();
            List<Action<ExtendedAirvent>> EventFuncs = new List<Action<ExtendedAirvent>>();

            public ExtendedAirvent(Program p, IMyAirVent vent)
            {
                P = p;
                this.vent = vent;
                lastVentState = vent.Depressurize;
            }

            public void Update()
            {
                ChangedThisUpdate = false;
                if (vent.Depressurize != lastVentState)
                {
                    ChangedThisUpdate = true;
                    foreach (var action in EventActions)
                    {
                        action();
                    }
                    foreach (var action in EventFuncs)
                    {
                        action(this);
                    }
                }
                lastVentState = vent.Depressurize;
            }

            public float GetOxygenLevel()
            {
                return vent.GetOxygenLevel();
            }

            public void Subscribe(Action action)
            {
                EventActions.Add(action);
            }

            public void SubscribeFunc(Action<ExtendedAirvent> func)
            {
                EventFuncs.Add(func);
            }
        }

        class ExtendedDoor
        {
            Program P;
            public IMyDoor door;
            TimeSpan autoCloseTime = TimeSpan.MaxValue;
            public float timeOpenExiting;
            public float timeOpenEntering;
            public bool autoClose;
            public bool ProgramOpening { get; private set; }
            public bool ProgramClosing { get; private set; }
            List<Action> EventActions = new List<Action>();
            List<Action<ExtendedDoor>> EventFuncs = new List<Action<ExtendedDoor>>();
            DoorStatus lastStatus;
            public bool isHangarDoor = false;
            public bool isManualDoor = false;
            private float autoCloseInSecondsOnceOpen = -1;
            public bool lockRequest = false;

            public ExtendedDoor(
                Program p,
                IMyDoor door,
                bool autoClose = true,
                float timeOpenEntering = .5f,
                float timeOpenExiting = 2f
            )
            {
                P = p;
                this.door = door;
                this.autoClose = autoClose;
                this.timeOpenEntering = timeOpenEntering;
                this.timeOpenExiting = timeOpenExiting;
                lastStatus = door.Status;
                door.CustomData = door.BlockDefinition.SubtypeId;
                if (
                    door is IMyAirtightHangarDoor
                    || door.BlockDefinition.SubtypeId.EndsWith("Gate")
                    || door.CustomName.Contains("#Hangar") // added this check
                )
                {
                    isHangarDoor = true;
                }
                if (General.ContainsExact((string)P.settings[ID.ManualTag].Value, door.CustomName))
                {
                    this.autoClose = false;
                    isManualDoor = true;
                }
            }

            public void Update()
            {
                if (autoClose)
                {
                    if (P.Time > autoCloseTime)
                    {
                        door.CloseDoor();
                        ProgramClosing = true;
                        autoCloseTime = TimeSpan.MaxValue;
                    }
                    else if (door.Status == DoorStatus.Open && autoCloseTime == TimeSpan.MaxValue)
                    {
                        if (timeOpenEntering >= 0)
                        {
                            autoCloseTime = P.Time + TimeSpan.FromSeconds(timeOpenEntering);
                        }
                    }
                }
                if (lockRequest && door.Status == DoorStatus.Closed)
                {
                    door.Enabled = false;
                    lockRequest = false;
                }
                if (door.Status != lastStatus)
                {
                    if (
                        autoClose
                        && autoCloseInSecondsOnceOpen >= 0
                        && door.Status == DoorStatus.Open
                    )
                    {
                        autoCloseTime = P.Time + TimeSpan.FromSeconds(autoCloseInSecondsOnceOpen);
                        autoCloseInSecondsOnceOpen = -1;
                    }
                    if (door.Status == DoorStatus.Closed)
                    {
                        ProgramOpening = false;
                    }
                    if (door.Status == DoorStatus.Open)
                    {
                        ProgramOpening = false;
                    }
                    foreach (var action in EventActions)
                    {
                        action();
                    }
                    foreach (var func in EventFuncs)
                    {
                        func(this);
                    }
                }
                lastStatus = door.Status;
            }

            public void Subscribe(Action action)
            {
                EventActions.Add(action);
            }

            public void SubscribeFunc(Action<ExtendedDoor> func)
            {
                EventFuncs.Add(func);
            }

            public void ProgramOpen()
            {
                door.OpenDoor();
                ProgramOpening = true;
                autoCloseInSecondsOnceOpen = timeOpenExiting;
            }

            public void ProgramOpen(float seconds)
            {
                door.OpenDoor();
                ProgramOpening = true;
                autoCloseInSecondsOnceOpen = seconds;
            }
        }

        public class FixedWidthText
        {
            private List<string> Text;
            public int Width { get; private set; }

            public FixedWidthText(int width)
            {
                Text = new List<string>();
                Width = width;
            }

            public void Clear()
            {
                Text.Clear();
            }

            public void Append(string t)
            {
                Text[Text.Count - 1] += t;
            }

            public void AppendLine()
            {
                Text.Add("");
            }

            public void AppendLine(string t)
            {
                Text.Add(t);
            }

            public void Combine(List<string> input)
            {
                Text.AddRange(input);
            }

            public List<string> GetRaw()
            {
                return Text;
            }

            public string GetText()
            {
                return GetText(Width);
            }

            public string GetText(int lineWidth)
            {
                string finalText = "";
                foreach (var line in Text)
                {
                    string rest = line;
                    if (rest.Length > lineWidth)
                    {
                        while (rest.Length > lineWidth)
                        {
                            string part = rest.Substring(0, lineWidth);
                            rest = rest.Substring(lineWidth);
                            for (int i = part.Length - 1; i > 0; i--)
                            {
                                if (part[i] == ' ')
                                {
                                    finalText += part.Substring(0, i) + "\n";
                                    rest = part.Substring(i + 1) + rest;
                                    break;
                                }
                            }
                        }
                    }
                    finalText += rest + "\n";
                }
                return finalText;
            }

            public static string Adjust(string text, int width)
            {
                string rest = text;
                string output = "";
                if (rest.Length > width)
                {
                    while (rest.Length > width)
                    {
                        string part = rest.Substring(0, width);
                        rest = rest.Substring(width);
                        for (int i = part.Length - 1; i > 0; i--)
                        {
                            if (part[i] == ' ')
                            {
                                output += part.Substring(0, i) + "\n";
                                rest = part.Substring(i + 1) + rest;
                                break;
                            }
                        }
                    }
                }
                output += rest;
                return output;
            }
        }

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

            public static string ToPercent(double part, double whole, int decimals = 1)
            {
                double result = (part / whole) * 100;
                return result.ToString("n" + decimals) + "%";
            }

            public static bool TryParseRGB(string[] input, out Color color)
            {
                color = Color.Black;
                int r,
                    g,
                    b;
                if (!Int32.TryParse(input[0], out r))
                    return false;
                if (!Int32.TryParse(input[1], out g))
                    return false;
                if (!Int32.TryParse(input[2], out b))
                    return false;
                color = new Color(r, g, b);
                return true;
            }
        }

        class Hangar : AirManagedAirlock
        {
            bool lastAttemptScoop = false;

            public Hangar(AirlockComponents components) : base(components)
            {
                foreach (var door in components.outer)
                {
                    door.SubscribeFunc(OuterDoorAction);
                    if (door.isHangarDoor)
                    {
                        door.timeOpenEntering = -1;
                        door.timeOpenExiting = -1;
                        door.autoClose = false;
                    }
                }
                foreach (var door in components.inner)
                {
                    door.SubscribeFunc(InnerDoorAction);
                    if (door.isHangarDoor)
                    {
                        door.timeOpenEntering = -1;
                        door.timeOpenExiting = -1;
                        door.autoClose = false;
                    }
                }
                foreach (var vent in components.extendedVents)
                {
                    vent.SubscribeFunc(VentAction);
                }
            }

            public override void Update()
            {
                base.Update();
                if (AirlockState == AirlockState2.AwatingOuterLock)
                {
                    if (OuterOpenCount <= 0)
                    {
                        Depressurize(false);
                        timeout =
                            components.P.Time
                            + TimeSpan.FromSeconds(components.secondsBeforeTimeout);
                        AirlockState = AirlockState2.Pressurizing;
                    }
                }
                else if (AirlockState == AirlockState2.AwatingInnerLock)
                {
                    if (InnerOpenCount <= 0)
                    {
                        Depressurize(true);
                        timeout =
                            components.P.Time
                            + TimeSpan.FromSeconds(components.secondsBeforeTimeout);
                        AirlockState = AirlockState2.Depressurizing;
                    }
                }
                if (AirlockState == AirlockState2.Depressurizing)
                {
                    currentOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                    if (components.P.Time > timeout)
                    {
                        if (
                            Math.Abs(currentOxygenLevel - startOxygenLevel)
                            < components.oxygenDifferenceRequired
                        )
                        {
                            currentOxygenLevel = 0;
                            timeout = TimeSpan.MaxValue;
                        }
                        else
                        {
                            startOxygenLevel = currentOxygenLevel;
                            timeout = components.P.Time + ventDeadline;
                        }
                    }
                    if (
                        currentOxygenLevel < 0.1
                        || tanksFullSkipDepressurization
                        || components.P.inAtmo
                        || maybeAtmoSkipDepressurization
                    )
                    {
                        EnableDoors(components.outer, true);
                        OpenAll(components.outer);
                        EnableVents(false);
                        if (components.P.Time > timeout)
                        {
                            errorStatus = "Depressurization failed";
                        }
                        else
                        {
                            errorStatus = "";
                        }
                        timeout = TimeSpan.MaxValue;
                        AirlockState = AirlockState2.OuterOpen;
                    }
                    StatusService(AirlockState);
                }
                else if (AirlockState == AirlockState2.Pressurizing)
                {
                    if (startOxygenLevel > 0.8)
                    {
                        maybeAtmoSkipDepressurization = true;
                    }
                    else
                    {
                        maybeAtmoSkipDepressurization = false;
                    }
                    currentOxygenLevel = components.extendedVents[0].GetOxygenLevel();
                    if (components.P.Time > timeout)
                    {
                        if (
                            Math.Abs(currentOxygenLevel - startOxygenLevel)
                            < components.oxygenDifferenceRequired
                        )
                        {
                            currentOxygenLevel = 1;
                            timeout = TimeSpan.MaxValue;
                        }
                        else
                        {
                            startOxygenLevel = currentOxygenLevel;
                            timeout = components.P.Time + ventDeadline;
                        }
                    }
                    if (currentOxygenLevel > 0.9 || components.P.inAtmo)
                    {
                        EnableDoors(components.inner, true);
                        OpenAll(components.inner);
                        if (components.P.Time > timeout)
                        {
                            errorStatus = "Pressurization failed";
                        }
                        else
                        {
                            errorStatus = "";
                        }
                        timeout = TimeSpan.MaxValue;
                        AirlockState = AirlockState2.InnerOpen;
                    }
                    StatusService(AirlockState);
                }
                if (AirlockState == AirlockState2.Unknown)
                {
                    if (components.extendedVents[0].Depressurize)
                    {
                        Depressurize(true);
                        EnableVents(false);
                        if (InnerOpenCount <= 0)
                        {
                            AirlockState = AirlockState2.OuterOpen;
                        }
                        else
                        {
                            SendLockRequest(components.inner);
                            CloseAll(components.inner, true);
                            AirlockState = AirlockState2.AwatingInnerLock;
                        }
                    }
                    else
                    {
                        Depressurize(false);
                        if (OuterOpenCount <= 0)
                        {
                            AirlockState = AirlockState2.InnerOpen;
                        }
                        else
                        {
                            SendLockRequest(components.outer);
                            CloseAll(components.outer, true);
                            AirlockState = AirlockState2.AwatingOuterLock;
                        }
                    }
                }
                if (AirlockState == AirlockState2.OuterOpen)
                {
                    if (attemptAirScoop)
                    {
                        if (attemptAirScoop != lastAttemptScoop)
                        {
                            EnableVents(true);
                            lastAttemptScoop = attemptAirScoop;
                        }
                    }
                    else if (attemptAirScoop == false)
                    {
                        if (attemptAirScoop != lastAttemptScoop)
                        {
                            EnableVents(false);
                            lastAttemptScoop = attemptAirScoop;
                        }
                    }
                    if (components.P.inAtmoChanged)
                    {
                        components.P.Me.CustomData += "\n in atmo changed: " + components.P.inAtmo;
                        if (components.P.inAtmo == false)
                        {
                            CloseAll(components.inner, true);
                            SendLockRequest(components.inner);
                            AirlockState = AirlockState2.AwatingInnerLock;
                        }
                        else
                        {
                            EnableDoors(components.inner, true);
                        }
                    }
                }
            }

            public override void OuterDoorAction(ExtendedDoor door)
            {
                base.OuterDoorAction(door);
            }

            public override void InnerDoorAction(ExtendedDoor door)
            {
                base.InnerDoorAction(door);
            }

            public void VentAction(ExtendedAirvent vent)
            {
                if (vent.Depressurize)
                {
                    vent.Depressurize = false;
                    SendLockRequest(components.inner);
                    CloseAll(components.inner, true);
                    AirlockState = AirlockState2.AwatingInnerLock;
                }
                else
                {
                    vent.Depressurize = true;
                    SendLockRequest(components.outer);
                    CloseAll(components.outer, true);
                    AirlockState = AirlockState2.AwatingOuterLock;
                }
            }

            public override void StatusService(AirlockState2 state)
            {
                switch (state)
                {
                    case AirlockState2.InnerOpen:
                    case AirlockState2.OuterOpen:
                        components.SetLightsIdle();
                        break;
                    case AirlockState2.AwatingInnerLock:
                        components.TriggerOuterTimers();
                        components.SetLightsWorking();
                        break;
                    case AirlockState2.AwatingOuterLock:
                        components.TriggerInnerTimers();
                        components.SetLightsWorking();
                        break;
                    case AirlockState2.Unknown:
                        components.SetLightsWorking();
                        break;
                }
                if (components.statusDisplay == null)
                    return;
                switch (state)
                {
                    case AirlockState2.InnerOpen:
                        if (errorStatus.Length > 0)
                        {
                            components.statusDisplay.Update(errorStatus, true);
                        }
                        else
                        {
                            if (components.P.inAtmo)
                            {
                                components.statusDisplay.Update("Inner open - Atmo mode");
                            }
                            else
                            {
                                components.statusDisplay.Update("Inner open");
                            }
                        }
                        break;
                    case AirlockState2.OuterOpen:
                        if (errorStatus.Length > 0)
                        {
                            components.statusDisplay.Update(errorStatus, true);
                        }
                        else
                        {
                            if (components.P.inAtmo)
                            {
                                components.statusDisplay.Update("Outer open - Atmo mode");
                            }
                            else
                            {
                                components.statusDisplay.Update("Outer open");
                            }
                        }
                        break;
                    case AirlockState2.AwatingOuterLock:
                        components.statusDisplay.Update("Locking outer");
                        break;
                    case AirlockState2.AwatingInnerLock:
                        components.statusDisplay.Update("Locking inner");
                        break;
                    case AirlockState2.Pressurizing:
                        components.statusDisplay.Update("Pressurizing");
                        break;
                    case AirlockState2.Depressurizing:
                        components.statusDisplay.Update("Depressurizing");
                        break;
                    case AirlockState2.Unknown:
                        components.statusDisplay.Update("Setup in progress");
                        break;
                    default:
                        break;
                }
            }
        }

        public TimeSpan Time { get; private set; }
        ExecutionTime ExeTime;
        ulong runCount = 0;
        List<ExtendedDoor> ExtendedDoors = new List<ExtendedDoor>();
        List<TinyAirlock> TinyAirlocks = new List<TinyAirlock>();
        List<AdvancedAirlock> AdvancedAirlocks = new List<AdvancedAirlock>();
        List<Hangar> Hangars = new List<Hangar>();
        List<SimpleGroupAirlock> SimpleGroupAirlocks = new List<SimpleGroupAirlock>();
        List<IMyTextPanel> StatusLCDs = new List<IMyTextPanel>();
        public IMyShipController shipController;
        public double altitude = 0;
        public bool altitudeAccurate { get; private set; } = false;
        public bool inAtmo { get; private set; } = false;
        public bool inAtmoChanged = true;
        public IMyTextPanel debugLCD;
        string airlockTag = "#AL";
        string ignoreTag = "#Ignore";
        string hangartag = "#Hangar";
        float timeOpenExiting;
        float timeOpenEntering;
        double inAtmoDisableAltitude = 5000;
        public Dictionary<ID, Setting> settings;
        public Dictionary<Str, string> strings;
        private List<TimedMessage> timedMessages = new List<TimedMessage>();
        string detailedInfoText = "";
        string finalDetailedInfoText = "";
        int doorCount = 0;
        int tinyAirlockCount = 0;
        int smartAirlockCount = 0;
        int groupAirlockCount = 0;
        int hangarCount = 0;
        int simpleGroupCount = 0;
        int hangarAirSystemsCount = 0;
        int groupAirlockAirSystemsCount = 0;
        IEnumerator<bool> InitStateMachine;
        int MaxInstructionsPerTick;
        bool Initialized = false;
        int initCounter = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;
            ExeTime = new ExecutionTime(this);
            MaxInstructionsPerTick = Runtime.MaxInstructionCount / 4;
            InitStateMachine = Init();
            debugLCD = GridTerminalSystem.GetBlockWithName("ALDEBUG") as IMyTextPanel;
            if (debugLCD != null)
            {
                debugLCD.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            }
            strings = new Dictionary<Str, string>
            {
                { Str.ScriptName, "Aggressive airlocks" },
                { Str.AirlockTag, "Airlock tag" },
                { Str.HangarTag, "Hangar tag" },
                { Str.SensorTag, "Oxygen sensor tag" },
                { Str.IgnoreTag, "Ignore door tag" },
                { Str.ManualTag, "Manual door tag" },
                { Str.AutoCloseDelayExiting, "Auto close delay exiting (s)" },
                { Str.AutoCloseDelayEntering, "Auto close delay entering (s)" },
                { Str.AutoCloseDelayRegularDoors, "Auto close delay regular doors (s)" },
                { Str.Timeout, "[Advanced] Timeout (s)" },
                { Str.EnableRegularDoors, "Auto close regular doors" },
                { Str.EnableTinyAirlocks, "Enable Tiny Airlocks" },
                { Str.EnableSmartAirlocks, "Enable Smart Airlocks" },
                { Str.EnableGroupAirlocks, "Enable Group Airlocks (incl. Simple and Hangar)" },
                { Str.DefaultLampColor, "Airlock free light color" },
                { Str.ChangingLampColor, "Airlock in use light color" },
                { Str.ShowProblemsOnHud, "Show airlocks with problems on HUD" },
                { Str.Settings, "settings" },
                {
                    Str.SettingsInstructions,
                    "To change settings: Edit the value after the colon, then send the command 'Update' to the script."
                },
                {
                    Str.CommandsInstructions,
                    "To send a command, enter it as an argument in the programmable block and press run. (Can also be done via an action, e.g. in a button)."
                },
                { Str.OxygenDifference, "[Advanced] Timeout oxygen delta (%)" },
                { Str.SmartAirlock, "Smart airlock" },
                { Str.GroupAirlock, "Airlock" },
                { Str.Hangar, "Hangar" },
                { Str.Error, "Error" },
                { Str.SetupLog, "Setup log" },
                { Str.SeeCustomData, "Settings: See custom data." },
                {
                    Str.ArgumentNotUnderstood,
                    "was not understood. Avalible arguments are: 'update', 'atmo', 'atmo on', 'atmo off'."
                },
                { Str.InAtmo, "Atmosphere mode enabled" },
                { Str.InAtmoDisableAltitude, "Auto disable atmo mode above (m)" }
            };
        }

        IEnumerator<bool> Init()
        {
            settings = new Dictionary<ID, Setting>
            {
                { ID.AirlockTag, new Setting(strings[Str.AirlockTag], "#AL") },
                { ID.HangarTag, new Setting(strings[Str.HangarTag], "#Hangar") },
                { ID.IgnoreTag, new Setting(strings[Str.IgnoreTag], "#Ignore") },
                { ID.ManualTag, new Setting(strings[Str.ManualTag], "#Manual") },
                {
                    ID.AutoCloseDelayEntering,
                    new Setting(strings[Str.AutoCloseDelayEntering], 0.5f, 1)
                },
                { ID.AutoCloseDelayExiting, new Setting(strings[Str.AutoCloseDelayExiting], 2.0f) },
                { ID.Timeout, new Setting(strings[Str.Timeout], 2f) },
                { ID.OxygenDifference, new Setting(strings[Str.OxygenDifference], 20f) },
                { ID.EnableRegularDoors, new Setting(strings[Str.EnableRegularDoors], true, 1) },
                { ID.DefaultLampColor, new Setting(strings[Str.DefaultLampColor], Color.Green, 1) },
                { ID.ChangingLampColor, new Setting(strings[Str.ChangingLampColor], Color.Violet) }
            };
            Settings.ParseSettings(Me.CustomData, settings);
            var text = new FixedWidthText(70);
            text.AppendLine(strings[Str.ScriptName] + " " + strings[Str.Settings]);
            text.AppendLine(
                "----------------------------------------------------------------------"
            );
            text.AppendLine(strings[Str.SettingsInstructions]);
            text.AppendLine(strings[Str.CommandsInstructions]);
            text.AppendLine(
                "----------------------------------------------------------------------"
            );
            text.AppendLine();
            Settings.GenerateCustomData(settings, text);
            text.AppendLine();
            text.AppendLine();
            text.AppendLine(strings[Str.SetupLog]);
            text.AppendLine(
                "----------------------------------------------------------------------"
            );
            hangartag = (string)settings[ID.HangarTag].Value;
            airlockTag = (string)settings[ID.AirlockTag].Value;
            hangartag = (string)settings[ID.HangarTag].Value;
            ignoreTag = (string)settings[ID.IgnoreTag].Value;
            timeOpenExiting = (float)settings[ID.AutoCloseDelayExiting].Value;
            timeOpenEntering = (float)settings[ID.AutoCloseDelayEntering].Value;
            doorCount = 0;
            tinyAirlockCount = 0;
            smartAirlockCount = 0;
            groupAirlockCount = 0;
            hangarCount = 0;
            simpleGroupCount = 0;
            hangarAirSystemsCount = 0;
            groupAirlockAirSystemsCount = 0;
            ExtendedDoors.Clear();
            TinyAirlocks.Clear();
            AdvancedAirlocks.Clear();
            Hangars.Clear();
            SimpleGroupAirlocks.Clear();
            StatusLCDs.Clear();
            var allBlocks = new List<IMyTerminalBlock>();
            var allDoors = new List<IMyDoor>();
            var allVents = new List<IMyAirVent>();
            var allGasGenerators = new List<IMyGasGenerator>();
            var allOxygenTanks = new List<IMyGasTank>();
            var allLCDs = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocks(allBlocks);
            for (int i = allBlocks.Count - 1; i >= 0; i--)
            {
                if (OverInstructionLimit())
                    yield return true;
                if (!allBlocks[i].IsSameConstructAs(Me))
                {
                    allBlocks.RemoveAt(i);
                    continue;
                }
                if (General.HasTag(ignoreTag, allBlocks[i].CustomName))
                {
                    allBlocks.RemoveAt(i);
                    continue;
                }
                if (allBlocks[i] is IMyDoor)
                {
                    allDoors.Add(allBlocks[i] as IMyDoor);
                }
                else if (allBlocks[i] is IMyAirVent)
                {
                    allVents.Add(allBlocks[i] as IMyAirVent);
                }
                else if (allBlocks[i] is IMyTextPanel)
                {
                    allLCDs.Add(allBlocks[i] as IMyTextPanel);
                }
            }
            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(groups);
            if (groups.Count > 0)
            {
                foreach (var group in groups)
                {
                    if (OverInstructionLimit())
                        yield return true;
                    bool hasHangarTag = false;
                    var blocks = new List<IMyTerminalBlock>();
                    var outer = new List<IMyDoor>();
                    var inner = new List<IMyDoor>();
                    var vents = new List<IMyAirVent>();
                    var panels = new List<IMyTextPanel>();
                    var soundBlocks = new List<IMySoundBlock>();
                    var lights = new List<IMyLightingBlock>();
                    var tanks = new List<IMyGasTank>();
                    var generators = new List<IMyGasGenerator>();
                    var oxygenFarms = new List<IMyFunctionalBlock>();
                    var innerTimers = new List<IMyTimerBlock>();
                    var outerTimers = new List<IMyTimerBlock>();
                    group.GetBlocks(blocks);
                    foreach (var block in blocks)
                    {
                        if (!block.IsSameConstructAs(Me))
                        {
                            continue;
                        }
                        if (General.HasTag(ignoreTag, block.CustomName))
                        {
                            continue;
                        }
                        if (block is IMyDoor)
                        {
                            if (General.HasTag(airlockTag, block.CustomName))
                            {
                                outer.Add(block as IMyDoor);
                            }
                            else if (General.HasTag(hangartag, block.CustomName))
                            {
                                outer.Add(block as IMyDoor);
                                hasHangarTag = true;
                            }
                            else
                            {
                                inner.Add(block as IMyDoor);
                            }
                        }
                        else if (block is IMyAirVent)
                        {
                            vents.Add(block as IMyAirVent);
                        }
                        else if (block is IMyTextPanel)
                        {
                            panels.Add(block as IMyTextPanel);
                            allLCDs.Remove(block as IMyTextPanel);
                        }
                        else if (block is IMySoundBlock)
                        {
                            soundBlocks.Add(block as IMySoundBlock);
                        }
                        else if (block is IMyLightingBlock)
                        {
                            lights.Add(block as IMyLightingBlock);
                        }
                        else if (block is IMyGasTank)
                        {
                            if (block.BlockDefinition.SubtypeId == "")
                            {
                                tanks.Add(block as IMyGasTank);
                            }
                        }
                        else if (block is IMyGasGenerator)
                        {
                            generators.Add(block as IMyGasGenerator);
                        }
                        else if (block is IMyOxygenFarm)
                        {
                            oxygenFarms.Add(block as IMyFunctionalBlock);
                        }
                        else if (block is IMyTimerBlock)
                        {
                            if (
                                General.HasTag(airlockTag, block.CustomName)
                                || General.HasTag(hangartag, block.CustomName)
                            )
                            {
                                outerTimers.Add(block as IMyTimerBlock);
                            }
                            else
                            {
                                innerTimers.Add(block as IMyTimerBlock);
                            }
                        }
                    }
                    if (vents.Count > 0)
                    {
                        if (!hasHangarTag && inner.Count > 0 && outer.Count > 0)
                        {
                            var data = new AirlockComponents(
                                this,
                                ExtendDoors(outer),
                                ExtendDoors(inner),
                                ExtendVents(vents)
                            );
                            if (tanks.Count > 0)
                            {
                                data.tanks = tanks;
                                if (generators.Count > 0)
                                    data.generators = generators;
                                if (oxygenFarms.Count > 0)
                                    data.farms = oxygenFarms;
                                groupAirlockAirSystemsCount++;
                            }
                            if (lights.Count > 0)
                            {
                                data.lights = lights;
                            }
                            if (panels.Count > 0)
                            {
                                data.statusDisplay = new AirlockStatusDisplay(
                                    this,
                                    panels,
                                    strings[Str.GroupAirlock] + " " + AdvancedAirlocks.Count + 1
                                );
                            }
                            if (outerTimers.Count > 0)
                            {
                                data.outerTimers = outerTimers;
                            }
                            if (innerTimers.Count > 0)
                            {
                                data.innerTimers = innerTimers;
                            }
                            AdvancedAirlocks.Add(new AdvancedAirlock(data));
                            groupAirlockCount++;
                            text.AppendLine(
                                "\n> Advanced airlock " + AdvancedAirlocks.Count + " added"
                            );
                            text.AppendLine("Type: Group airlock");
                            text.AppendLine(
                                $"{outer.Count} Outer door(s): {string.Join(", ", outer.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{inner.Count} Inner door(s): {string.Join(", ", inner.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{vents.Count} Airvents: {string.Join(", ", vents.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{tanks.Count} Oxygen tanks: {string.Join(", ", tanks.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{generators.Count} Oxygen generators: {string.Join(", ", generators.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{oxygenFarms.Count} Oxygen farms: {string.Join(", ", oxygenFarms.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{panels.Count} LCDs: {string.Join(", ", panels.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{lights.Count} Lights: {string.Join(", ", lights.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{outerTimers.Count} Outer timers: {string.Join(", ", outerTimers.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{innerTimers.Count} Inner timers: {string.Join(", ", innerTimers.Select(r => r.CustomName))}"
                            );
                        }
                        else if (hasHangarTag && outer.Count > 0)
                        {
                            var data = new AirlockComponents(
                                this,
                                ExtendDoors(outer),
                                ExtendDoors(inner),
                                ExtendVents(vents)
                            );
                            if (tanks.Count > 0)
                            {
                                data.tanks = tanks;
                                if (generators.Count > 0)
                                    data.generators = generators;
                                if (oxygenFarms.Count > 0)
                                    data.farms = oxygenFarms;
                                hangarAirSystemsCount++;
                            }
                            if (lights.Count > 0)
                            {
                                data.lights = lights;
                            }
                            if (panels.Count > 0)
                            {
                                data.statusDisplay = new AirlockStatusDisplay(
                                    this,
                                    panels,
                                    strings[Str.Hangar] + " " + Hangars.Count + 1
                                );
                            }
                            if (outerTimers.Count > 0)
                            {
                                data.outerTimers = outerTimers;
                            }
                            if (innerTimers.Count > 0)
                            {
                                data.innerTimers = innerTimers;
                            }
                            Hangars.Add(new Hangar(data));
                            hangarCount++;
                            text.AppendLine("\n> Hangar " + Hangars.Count + " added");
                            text.AppendLine("Type: Hangar");
                            text.AppendLine(
                                $"{outer.Count} Outer door(s): {string.Join(", ", outer.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{inner.Count} Inner door(s): {string.Join(", ", inner.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{vents.Count} Airvents: {string.Join(", ", vents.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{tanks.Count} Oxygen tanks: {string.Join(", ", tanks.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{generators.Count} Oxygen generators: {string.Join(", ", generators.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{oxygenFarms.Count} Oxygen farms: {string.Join(", ", oxygenFarms.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{panels.Count} LCDs: {string.Join(", ", panels.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{lights.Count} Lights: {string.Join(", ", lights.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{outerTimers.Count} Outer timers: {string.Join(", ", outerTimers.Select(r => r.CustomName))}"
                            );
                            text.AppendLine(
                                $"{innerTimers.Count} Inner timers: {string.Join(", ", innerTimers.Select(r => r.CustomName))}"
                            );
                        }
                        allDoors = allDoors.Except(outer).Except(inner).ToList();
                        allVents = allVents.Except(vents).ToList();
                    }
                    else
                    {
                        if (outer.Count > 0)
                        {
                            bool includesHangarDoors = false;
                            foreach (var door in outer)
                            {
                                if (door is IMyAirtightHangarDoor)
                                {
                                    includesHangarDoors = true;
                                    break;
                                }
                            }
                            if (!includesHangarDoors)
                            {
                                SimpleGroupAirlocks.Add(
                                    new SimpleGroupAirlock(
                                        this,
                                        ExtendDoors(outer),
                                        ExtendDoors(inner)
                                    )
                                );
                                simpleGroupCount++;
                                text.AppendLine(
                                    "\n> Simple Group airlock "
                                        + SimpleGroupAirlocks.Count
                                        + " added"
                                );
                                text.AppendLine($"{outer.Count} doors");
                                allDoors = allDoors.Except(outer).Except(inner).ToList();
                                allVents = allVents.Except(vents).ToList();
                            }
                        }
                    }
                }
            }
            for (int i = 0; i < allDoors.Count; i++)
            {
                if (OverInstructionLimit())
                    yield return true;
                for (int j = 0; j < allDoors.Count; j++)
                {
                    if (
                        allDoors[i].Position
                            + Base6Directions.GetIntVector(allDoors[i].Orientation.Forward)
                            == allDoors[j].Position
                        || allDoors[i].Position
                            - Base6Directions.GetIntVector(allDoors[i].Orientation.Forward)
                            == allDoors[j].Position
                    )
                    {
                        if (
                            allDoors[i].Orientation.Forward == allDoors[j].Orientation.Forward
                            || allDoors[i].Orientation.Forward
                                == Base6Directions.GetFlippedDirection(
                                    allDoors[j].Orientation.Forward
                                )
                        )
                        {
                            TinyAirlocks.Add(
                                new TinyAirlock(
                                    this,
                                    ExtendDoor(allDoors[i]),
                                    ExtendDoor(allDoors[j])
                                )
                            );
                            tinyAirlockCount++;
                            text.AppendLine("\n> Tiny airlock " + TinyAirlocks.Count + " added");
                            allDoors.RemoveAtFast(j);
                            allDoors.RemoveAtFast(i);
                            i--;
                            break;
                        }
                    }
                }
            }
            var outerDoors = new List<IMyDoor>();
            var innerDoors = new List<IMyDoor>();
            for (int i = 0; i < allDoors.Count; i++)
            {
                if (OverInstructionLimit())
                    yield return true;
                if (General.HasTag(airlockTag, allDoors[i].CustomName))
                {
                    outerDoors.Add(allDoors[i]);
                    allDoors.RemoveAtFast(i);
                    i--;
                }
                else
                {
                    innerDoors.Add(allDoors[i]);
                }
            }
            if (outerDoors.Count > 0 && innerDoors.Count > 0)
            {
                for (int i = 0; i < outerDoors.Count; i++)
                {
                    if (OverInstructionLimit())
                        yield return true;
                    int innerDoorIndex = -1;
                    int ventIndex = -1;
                    float distance = float.MaxValue;
                    float tempDist;
                    for (int j = 0; j < innerDoors.Count; j++)
                    {
                        tempDist = Vector3I.DistanceManhattan(
                            outerDoors[i].Position,
                            innerDoors[j].Position
                        );
                        if (tempDist > 0 && tempDist < distance)
                        {
                            distance = tempDist;
                            innerDoorIndex = j;
                        }
                    }
                    if (innerDoorIndex >= 0)
                    {
                        distance = float.MaxValue;
                        for (int j = 0; j < allVents.Count; j++)
                        {
                            tempDist = Vector3I.DistanceManhattan(
                                outerDoors[i].Position,
                                allVents[j].Position
                            );
                            if (tempDist < distance)
                            {
                                distance = tempDist;
                                ventIndex = j;
                            }
                        }
                        if (ventIndex >= 0)
                        {
                            var data = new AirlockComponents(
                                this,
                                ExtendDoor(outerDoors[i]),
                                ExtendDoor(innerDoors[innerDoorIndex]),
                                ExtendVent(allVents[ventIndex])
                            );
                            AdvancedAirlocks.Add(new AdvancedAirlock(data));
                            smartAirlockCount++;
                            text.AppendLine(
                                "\n> Advanced airlock " + AdvancedAirlocks.Count + " added"
                            );
                            text.AppendLine("Type: Smart airlock");
                            text.AppendLine("Outer door: " + outerDoors[i].CustomName);
                            text.AppendLine("Inner door: " + innerDoors[innerDoorIndex].CustomName);
                            text.AppendLine("Airvent: " + allVents[ventIndex].CustomName);
                            outerDoors.RemoveAtFast(i);
                            i--;
                            innerDoors.RemoveAtFast(innerDoorIndex);
                            allVents.RemoveAtFast(ventIndex);
                        }
                    }
                }
            }
            allDoors = innerDoors;
            if ((bool)settings[ID.EnableRegularDoors].Value)
            {
                foreach (var item in allDoors)
                {
                    if (
                        !General.ContainsExact(
                            (string)settings[ID.ManualTag].Value,
                            item.CustomName
                        )
                    )
                    {
                        ExtendedDoors.Add(ExtendDoor(item));
                    }
                }
                doorCount = ExtendedDoors.Count;
                text.AppendLine($"\n> {ExtendedDoors.Count} Regular doors added");
            }
            else
            {
                text.AppendLine($"\n> 0 Regular doors added");
            }
            for (int i = 0; i < allLCDs.Count; i++)
            {
                if (General.HasTag(airlockTag, allLCDs[i].CustomName))
                {
                    StatusLCDs.Add(allLCDs[i]);
                    allLCDs[i].ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                }
            }
            Me.CustomData = text.GetText();
            FixedWidthText detailedInfo = new FixedWidthText(30);
            detailedInfo.Clear();
            detailedInfo.AppendLine();
            detailedInfo.AppendLine(strings[Str.SeeCustomData]);
            detailedInfo.AppendLine();
            detailedInfo.AppendLine("Doors: " + doorCount);
            detailedInfo.AppendLine("Tiny airlocks: " + tinyAirlockCount);
            detailedInfo.AppendLine("Smart airlocks: " + smartAirlockCount);
            detailedInfo.AppendLine(
                "Group airlocks: " + groupAirlockCount + $" ({groupAirlockAirSystemsCount})"
            );
            detailedInfo.AppendLine("Hangars: " + hangarCount + $" ({hangarAirSystemsCount})");
            detailedInfo.AppendLine("Simple group airlocks: " + simpleGroupCount);
            detailedInfoText = detailedInfo.GetText();
            Initialized = true;
            yield return true;
        }

        public void Save() { }

        void Test()
        {
            var oxygenGens = new List<IMyGasGenerator>();
            GridTerminalSystem.GetBlocksOfType(oxygenGens);
            var oxygenTanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(oxygenTanks);
            var cargo = GridTerminalSystem.GetBlockWithName("Small Cargo Container");
            IMyInventory inv = cargo.GetInventory();
            foreach (var tank in oxygenTanks)
            {
                if (inv.IsConnectedTo(tank.GetInventory()))
                {
                    Echo(cargo.CustomName + " is connected to " + tank.CustomName + "\n");
                }
            }
            foreach (var gen in oxygenGens)
            {
                if (inv.IsConnectedTo(gen.GetInventory()))
                {
                    Echo(cargo.CustomName + " is connected to " + gen.CustomName + "\n");
                }
            }
        }

        void Test2()
        {
            var oxygenTanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(oxygenTanks);
            var cargo = GridTerminalSystem.GetBlockWithName("Small Cargo Container");
            IMyInventory inv = cargo.GetInventory();
            foreach (var tank in oxygenTanks)
            {
                if (inv.IsConnectedTo(tank.GetInventory()))
                {
                    Echo(cargo.CustomName + " is connected to " + tank.CustomName + "\n");
                }
            }
        }

        public void Main(string argument, UpdateType updateType)
        {
            Time = Time + Runtime.TimeSinceLastRun;
            runCount++;
            if (!Initialized)
            {
                initCounter++;
                Echo("Initializing." + new string('.', initCounter));
                if (InitStateMachine != null)
                {
                    if (!InitStateMachine.MoveNext())
                    {
                        InitStateMachine.Dispose();
                        InitStateMachine = null;
                    }
                    else if (!InitStateMachine.Current)
                    {
                        InitStateMachine.Dispose();
                        InitStateMachine = null;
                    }
                }
            }
            else
            {
                if ((updateType & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
                {
                    if (Initialized)
                    {
                        switch (argument.ToLower().Trim())
                        {
                            case "update":
                                initCounter = 0;
                                Initialized = false;
                                InitStateMachine = Init();
                                break;
                            case "atmo":
                                SetAtmo(!inAtmo);
                                break;
                            case "atmo off":
                                SetAtmo(false);
                                break;
                            case "atmo on":
                                SetAtmo(true);
                                break;
                            default:
                                timedMessages.Add(
                                    new TimedMessage(
                                        Time + TimeSpan.FromSeconds(7),
                                        "'" + argument + "' " + strings[Str.ArgumentNotUnderstood]
                                    )
                                );
                                Me.CustomData += FixedWidthText.Adjust(
                                    "\n> " + timedMessages.Last().message,
                                    70
                                );
                                break;
                        }
                    }
                }
                if ((updateType & UpdateType.Update10) != 0)
                {
                    if (shipController != null)
                    {
                        altitudeAccurate = shipController.TryGetPlanetElevation(
                            MyPlanetElevation.Sealevel,
                            out altitude
                        );
                        if (inAtmo && altitudeAccurate && altitude > inAtmoDisableAltitude)
                        {
                            SetAtmo(false);
                        }
                    }
                    foreach (var door in ExtendedDoors)
                    {
                        door.Update();
                    }
                    foreach (var airlock in TinyAirlocks)
                    {
                        airlock.Update();
                    }
                    foreach (var airlock in AdvancedAirlocks)
                    {
                        airlock.Update();
                    }
                    foreach (var hangar in Hangars)
                    {
                        hangar.Update();
                    }
                    foreach (var airlock in SimpleGroupAirlocks)
                    {
                        airlock.Update();
                    }
                    if (runCount % 5 == 0)
                    {
                        PrintCustomData();
                    }
                    inAtmoChanged = false;
                }
                if ((updateType & UpdateType.Update100) != 0)
                {
                    UpdateStatusLCD();
                }
            }
            ExeTime.Update();
        }

        private void PrintCustomData()
        {
            finalDetailedInfoText = "Blarg's " + strings[Str.ScriptName] + DotDotDot();
            finalDetailedInfoText +=
                "\nLoad (avg): "
                + General.ToPercent(ExeTime.GetAvrage(), Runtime.MaxInstructionCount)
                + ", "
                + ExeTime.GetAvrageTime().ToString("n2")
                + "ms";
            if (inAtmo)
                finalDetailedInfoText += "\n\nAtmosphere mode enabled";
            if (timedMessages.Count > 0)
            {
                for (int i = timedMessages.Count - 1; i >= 0; i--)
                {
                    if (Time > timedMessages[i].expiration)
                    {
                        timedMessages.RemoveAt(i);
                    }
                    else
                    {
                        finalDetailedInfoText +=
                            "\n\n" + FixedWidthText.Adjust(timedMessages[i].message, 30);
                    }
                }
            }
            finalDetailedInfoText += "\n" + detailedInfoText;
            Echo(finalDetailedInfoText);
            if (debugLCD != null)
            {
                debugLCD.WriteText(finalDetailedInfoText);
            }
        }

        private void UpdateStatusLCD()
        {
            if (StatusLCDs.Count == 0)
                return;
            string final = "Blarg's " + strings[Str.ScriptName];
            final +=
                "\nLoad (avg): "
                + General.ToPercent(ExeTime.GetAvrage(), Runtime.MaxInstructionCount)
                + ", "
                + ExeTime.GetAvrageTime().ToString("n2")
                + "ms";
            if (timedMessages.Count > 0)
            {
                for (int i = timedMessages.Count - 1; i >= 0; i--)
                {
                    if (Time > timedMessages[i].expiration)
                    {
                        timedMessages.RemoveAt(i);
                    }
                    else
                    {
                        final += "\n\n" + FixedWidthText.Adjust(timedMessages[i].message, 30);
                    }
                }
            }
            final += "\n" + detailedInfoText;
            for (int i = 0; i < StatusLCDs.Count; i++)
            {
                StatusLCDs[i].WriteText(final);
            }
        }

        public bool OverInstructionLimit()
        {
            return Runtime.CurrentInstructionCount > MaxInstructionsPerTick;
        }

        public void SetAtmo(bool value)
        {
            if (value == true && altitudeAccurate && altitude > inAtmoDisableAltitude)
            {
                return;
            }
            if (inAtmo == value)
            {
                return;
            }
            inAtmo = value;
            inAtmoChanged = true;
        }

        private ExtendedDoor ExtendDoor(IMyDoor door)
        {
            return new ExtendedDoor(this, door, true, timeOpenEntering, timeOpenExiting);
        }

        private List<ExtendedDoor> ExtendDoors(List<IMyDoor> doors)
        {
            var extendedDoors = new ExtendedDoor[doors.Count];
            for (int i = 0; i < doors.Count; i++)
            {
                extendedDoors[i] = ExtendDoor(doors[i]);
            }
            return extendedDoors.ToList();
        }

        private ExtendedAirvent ExtendVent(IMyAirVent vent)
        {
            return new ExtendedAirvent(this, vent);
        }

        private List<ExtendedAirvent> ExtendVents(List<IMyAirVent> vents)
        {
            var extendedVents = new ExtendedAirvent[vents.Count];
            for (int i = 0; i < vents.Count; i++)
            {
                extendedVents[i] = ExtendVent(vents[i]);
            }
            return extendedVents.ToList();
        }

        public string SerializeCustomNameList(List<IMyDoor> list)
        {
            string text = "";
            foreach (var item in list)
            {
                text += item.CustomName + ", ";
            }
            return text;
        }

        public string SerializeCustomNameList(List<IMyAirVent> list)
        {
            string text = "";
            foreach (var item in list)
            {
                text += item.CustomName + ", ";
            }
            return text;
        }

        public string SerializeCustomNameList(List<IMyTextPanel> list)
        {
            string text = "";
            foreach (var item in list)
            {
                text += item.CustomName + ", ";
            }
            return text;
        }

        string[] dots = { ".", "..", "..." };
        int dotIndex = -1;

        private string DotDotDot()
        {
            dotIndex++;
            if (dotIndex >= dots.Length)
            {
                dotIndex = 0;
            }
            return dots[dotIndex];
        }

        class TimedMessage
        {
            public TimeSpan expiration;
            public string message;

            public TimedMessage(TimeSpan expiration, string message)
            {
                this.expiration = expiration;
                this.message = message;
            }
        }

        public enum ID
        {
            AutoCloseDelayExiting,
            AutoCloseDelayEntering,
            AutoCloseDelayRegularDoors,
            Timeout,
            AirlockTag,
            HangarTag,
            SensorTag,
            IgnoreTag,
            ManualTag,
            EnableRegularDoors,
            EnableTinyAirlocks,
            EnableSmartAirlocks,
            EnableGroupAirlocks,
            DefaultLampColor,
            ChangingLampColor,
            ShowProblemsOnHud,
            OxygenDifference,
            InAtmo,
            InAtmoDisableAltitude
        }

        public enum Str
        {
            ScriptName,
            AutoCloseDelayExiting,
            AutoCloseDelayEntering,
            AutoCloseDelayRegularDoors,
            Timeout,
            AirlockTag,
            HangarTag,
            SensorTag,
            IgnoreTag,
            ManualTag,
            EnableRegularDoors,
            EnableTinyAirlocks,
            EnableSmartAirlocks,
            EnableGroupAirlocks,
            DefaultLampColor,
            ChangingLampColor,
            ShowProblemsOnHud,
            Settings,
            SettingsInstructions,
            CommandsInstructions,
            OxygenDifference,
            SmartAirlock,
            GroupAirlock,
            Hangar,
            Error,
            SetupLog,
            SeeCustomData,
            ArgumentNotUnderstood,
            InAtmo,
            InAtmoDisableAltitude
        }

        class Settings
        {
            public static void ParseSettings(string data, Dictionary<ID, Setting> settings)
            {
                if (data.Length == 0)
                    return;
                var lines = data.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var keys = new List<ID>(settings.Keys);
                    foreach (var key in keys)
                    {
                        if (lines[i].StartsWith(settings[key].Text))
                        {
                            var parts = lines[i].Split(new char[] { ':' }, 2);
                            if (!string.IsNullOrEmpty(parts[1]))
                            {
                                var val = parts[1].Trim();
                                if (settings[key].Value is bool)
                                {
                                    if (val.ToLower() == "yes")
                                    {
                                        settings[key].Value = true;
                                    }
                                    else if (val.ToLower() == "no")
                                    {
                                        settings[key].Value = false;
                                    }
                                }
                                else if (settings[key].Value is int)
                                {
                                    int temp = 0;
                                    if (int.TryParse(val, out temp))
                                    {
                                        settings[key].Value = temp;
                                    }
                                }
                                else if (settings[key].Value is float)
                                {
                                    float temp = 0;
                                    if (float.TryParse(val, out temp))
                                    {
                                        settings[key].Value = temp;
                                    }
                                }
                                else if (settings[key].Value is double)
                                {
                                    double temp = 0;
                                    if (double.TryParse(val, out temp))
                                    {
                                        settings[key].Value = temp;
                                    }
                                }
                                else if (settings[key].Value is string)
                                {
                                    if (
                                        !string.IsNullOrEmpty(val)
                                        && !string.IsNullOrWhiteSpace(val)
                                    )
                                    {
                                        settings[key].Value = val;
                                    }
                                }
                                else if (settings[key].Value is Color)
                                {
                                    if (
                                        !string.IsNullOrEmpty(val)
                                        && !string.IsNullOrWhiteSpace(val)
                                    )
                                    {
                                        val = val.ToLower();
                                        if (
                                            val.Contains("r:")
                                            && val.Contains("g:")
                                            && val.Contains("b:")
                                        )
                                        {
                                            var split = val.Split(',');
                                            if (split.Length == 3)
                                            {
                                                for (int j = 0; j < split.Length; j++)
                                                {
                                                    split[j] =
                                                        System.Text.RegularExpressions.Regex.Replace(
                                                            split[j],
                                                            "[^0-9.]",
                                                            ""
                                                        );
                                                }
                                                Color color;
                                                if (General.TryParseRGB(split, out color))
                                                {
                                                    settings[key].Value = color;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public static string GenerateCustomData(
                Dictionary<ID, Setting> settings,
                FixedWidthText text
            )
            {
                foreach (var setting in settings.Values)
                {
                    for (int i = 0; i < setting.SpaceAbove; i++)
                    {
                        text.AppendLine();
                    }
                    text.AppendLine(setting.Text + ": " + SettingToString(setting.Value));
                }
                return text.GetText();
            }

            private static string SettingToString(object input)
            {
                if (input is bool)
                {
                    return (bool)input ? "yes" : "no";
                }
                if (input is Color)
                {
                    var color = (Color)input;
                    return "R:" + color.R + ", G:" + color.G + ", B:" + color.B;
                }
                return input.ToString();
            }

            public static string ChangeInCustomData(
                string data,
                string settingText,
                object newValue
            )
            {
                var lines = data.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(settingText))
                    {
                        var parts = lines[i].Split(new char[] { ':' }, 2);
                        if (!string.IsNullOrEmpty(parts[1]))
                        {
                            parts[1] = " " + SettingToString(newValue);
                            lines[i] = parts[0] + ":" + parts[1];
                            return string.Join("\n", lines);
                        }
                    }
                }
                return data;
            }
        }

        public class Setting
        {
            public string Text;
            public object Value { get; set; }
            public int SpaceAbove;

            public Setting(string text, object value, int spaceAbove = 0)
            {
                Text = text;
                Value = value;
                SpaceAbove = spaceAbove;
            }
        }

        class SimpleGroupAirlock
        {
            Program P;
            ExtendedDoor[] outer;
            ExtendedDoor[] inner;
            bool outerChange = false;
            bool innerChange = false;
            int outerOpen = 0;
            int innerOpen = 0;
            bool soloMode = false;

            public SimpleGroupAirlock(Program p, List<ExtendedDoor> outer, List<ExtendedDoor> inner)
            {
                P = p;
                this.outer = outer.ToArray();
                this.inner = inner.ToArray();
                if (this.inner.Length == 0)
                {
                    soloMode = true;
                    for (int i = 0; i < this.outer.Length; i++)
                    {
                        this.outer[i].SubscribeFunc(OuterSoloFunc);
                        this.outer[i].door.Enabled = true;
                    }
                }
                else
                {
                    for (int i = 0; i < this.outer.Length; i++)
                    {
                        this.outer[i].SubscribeFunc(OuterFunc);
                        this.outer[i].door.Enabled = true;
                    }
                    for (int i = 0; i < this.inner.Length; i++)
                    {
                        this.inner[i].SubscribeFunc(InnerFunc);
                        this.inner[i].door.Enabled = true;
                    }
                }
            }

            public void Update()
            {
                outerChange = false;
                innerChange = false;
                for (int i = 0; i < outer.Length; i++)
                {
                    outer[i].Update();
                }
                for (int i = 0; i < inner.Length; i++)
                {
                    inner[i].Update();
                }
                if (!soloMode)
                {
                    if (outerChange && outerOpen == 0)
                    {
                        EnableDoors(inner, true);
                    }
                    if (innerChange && innerOpen == 0)
                    {
                        EnableDoors(outer, true);
                    }
                }
            }

            public void EnableDoors(ExtendedDoor[] doors, bool enable)
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    doors[i].door.Enabled = enable;
                }
            }

            public void OuterFunc(ExtendedDoor door)
            {
                outerChange = true;
                if (door.door.Status == DoorStatus.Opening)
                {
                    outerOpen++;
                    EnableDoors(inner, false);
                }
                if (door.door.Status == DoorStatus.Closed)
                {
                    outerOpen--;
                }
            }

            public void InnerFunc(ExtendedDoor door)
            {
                innerChange = true;
                if (door.door.Status == DoorStatus.Opening)
                {
                    innerOpen++;
                    EnableDoors(outer, false);
                }
                if (door.door.Status == DoorStatus.Closed)
                {
                    innerOpen--;
                }
            }

            public void OuterSoloFunc(ExtendedDoor door)
            {
                outerChange = true;
                if (door.door.Status == DoorStatus.Opening)
                {
                    for (int i = 0; i < outer.Length; i++)
                    {
                        if (outer[i] != door)
                        {
                            outer[i].door.Enabled = false;
                        }
                    }
                }
                if (door.door.Status == DoorStatus.Closed)
                {
                    for (int i = 0; i < outer.Length; i++)
                    {
                        outer[i].door.Enabled = true;
                    }
                }
            }
        }

        class TinyAirlock
        {
            Program P;
            ExtendedDoor door1;
            ExtendedDoor door2;
            bool openRequest = false;

            public TinyAirlock(Program p, ExtendedDoor door1, ExtendedDoor door2)
            {
                P = p;
                this.door1 = door1;
                this.door2 = door2;
                door1.Subscribe(Door1Action);
                door2.Subscribe(Door2Action);
            }

            public void Update()
            {
                door1.Update();
                door2.Update();
            }

            public void Door1Action()
            {
                DoorAction(door1, door2);
            }

            public void Door2Action()
            {
                DoorAction(door2, door1);
            }

            private void DoorAction(ExtendedDoor me, ExtendedDoor other)
            {
                if (me.door.Status == DoorStatus.Opening && other.door.Status == DoorStatus.Opening)
                {
                    me.door.CloseDoor();
                    other.door.CloseDoor();
                }
                else if (me.door.Status == DoorStatus.Opening && !me.ProgramOpening)
                {
                    if (!other.isManualDoor)
                        openRequest = true;
                    other.door.Enabled = false;
                }
                else if (
                    me.door.Status == DoorStatus.Closed
                    && other.door.Enabled == false
                    && openRequest
                )
                {
                    openRequest = false;
                    other.door.Enabled = true;
                    me.door.Enabled = false;
                    other.ProgramOpen();
                }
                else if (
                    me.door.Status == DoorStatus.Closed && other.door.Status == DoorStatus.Closed
                )
                {
                    me.door.Enabled = true;
                    other.door.Enabled = true;
                }
            }
        }
    }
}
