//   AnimatedConverterBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AT_Utils
{
    public abstract class AnimatedConverterBase : PartModule
    {
        #region Configuration
        [KSPField] public string Title             = "Converter";
        [KSPField] public string StartEventGUIName = "Start";
        [KSPField] public string StopEventGUIName  = "Stop";
        [KSPField] public string ActionGUIName     = "Toggle";
        [KSPField] public float  EnergyConsumption = 50f;
        [KSPField] public float  RatesMultiplier   = 1f;
        [KSPField] public float  MinimumRate       = 0.1f;
        [KSPField] public float  Acceleration      = 1f; //lerp fraction per second
        [KSPField] public float  HeatProduction    = 0f;
        [KSPField] public bool   SelfSustaining;
        #endregion

        #region State
        [KSPField(isPersistant = true)] public bool Converting;
        [KSPField(isPersistant = true)] public bool ShuttingOff;

        [KSPField(guiActive = true, guiName = "Temperature", guiUnits = "C", guiFormat = "F2")]
        public float Temperature;

        [KSPField(guiActiveEditor = true, guiName = "Energy Consumption", guiUnits = "/s", guiFormat = "F2")]
        public float CurrentEnergyDemand;
        protected ResourcePump socket;
        protected float consumption_rate;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Rate", guiFormat = "P1")]
        public float Rate;
        protected float last_rate;
        protected float next_rate = 1f;
        protected bool  above_threshold { get { return consumption_rate >= MinimumRate || Rate >= 0.01f; }}

        protected readonly List<AnimatedConverterBase> other_converters = new List<AnimatedConverterBase>();
        #endregion

        #region FX
        //animation
        [KSPField] public string AnimatorID = "_none_";
        protected MultiAnimator animator;
        #endregion

        public override string GetInfo()
        { 
            var info = "";
            info += Title+":\n";
            update_energy_demand();
            if(CurrentEnergyDemand > 0)
                info += string.Format("Energy Consumption: {0:F2}/sec\n", CurrentEnergyDemand);
            info += string.Format("Minimum Rate: {0:P1}\n", MinimumRate); 
            return info;
        }

        protected bool some_working
        {
            get
            {
                var working = false;
                foreach(var c in other_converters)
                { working |= c.Converting; if(working) break; }
                return working;
            }
        }

        public override void OnActive() 
        { 
            if(!string.IsNullOrEmpty(part.stagingIcon)) 
                StartConversion(); 
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            socket = part.CreateSocket();
            //get other converters
            if(AnimatorID != "_none_")
                other_converters.AddRange(from c in part.Modules.OfType<AnimatedConverterBase>()
                                          where c != this && c.AnimatorID == AnimatorID select c);
            //initialize Animator
            animator = part.GetAnimator(AnimatorID);
            //setup GUI fields
            Fields["Temperature"].guiActive       = HeatProduction > 0;
            Fields["CurrentEnergyDemand"].guiName = Title+" Uses";
            Fields["Rate"].guiName                = Title+" Rate";
            Events["StartConversion"].guiName     = StartEventGUIName+" "+Title;
            Events["StopConversion"].guiName      = StopEventGUIName+" "+Title;
            Actions["ToggleConversion"].guiName   = ActionGUIName+" "+Title;
            //update state
            update_energy_demand();
            update_events();
            StartCoroutine(slow_update());
        }

        public virtual void SetRatesMultiplier(float mult)
        { 
            RatesMultiplier = mult; 
            update_energy_demand();
        }

        void update_energy_demand()
        { CurrentEnergyDemand = RatesMultiplier * EnergyConsumption; }

        #region Conversion
        protected abstract bool can_convert(bool report = false);
        protected abstract bool convert();
        protected abstract void on_start_conversion();
        protected abstract void on_stop_conversion();

        protected bool consume_energy()
        {
            if(ShuttingOff) 
                consumption_rate = 0;
            else if(SelfSustaining && Rate >= MinimumRate)
                consumption_rate = 1;
            else
            {
                socket.RequestTransfer(CurrentEnergyDemand*TimeWarp.fixedDeltaTime);
                if(!socket.TransferResource()) return false;
                consumption_rate = socket.Ratio;
                if(consumption_rate < MinimumRate) 
                {
                    Utils.Message("Not enough energy");
                    socket.Clear();
                }
            }
            update_rate(Mathf.Min(consumption_rate, next_rate));
            return true;
        }

        protected void update_rate(float new_rate)
        { Rate = Mathf.Lerp(Rate, new_rate, Acceleration*TimeWarp.fixedDeltaTime); }

        protected void produce_heat() 
        { 
            Temperature = (float)part.temperature;
            double kilowatts = (double)HeatProduction * Rate * vessel.VesselValues.HeatProduction.value * PhysicsGlobals.InternalHeatProductionFactor * part.thermalMass;
            part.AddThermalFlux(kilowatts);
        }

        public void FixedUpdate()
        { 
            if(!Converting) return;
            if(!convert()) 
            {
                Rate = 0;
                Converting = false; 
                ShuttingOff = false;
                on_stop_conversion();
                update_events();
                return;
            }
            if(HeatProduction > 0) produce_heat();
        }
        #endregion

        IEnumerator<YieldInstruction> slow_update()
        {
            while(true)
            {
                if(!Rate.Equals(last_rate))
                {
                    if(animator != null) 
                        animator.SetSpeedMultiplier(Rate);
                    last_rate = Rate;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        #region Events & Actions
        protected void update_events()
        {
            var act = Converting && !ShuttingOff;
            Events["StartConversion"].active = !act;
            Events["StopConversion"].active  =  act;
            if(Converting) animator.Open();
            else if(!some_working) animator.Close();
        }

        [KSPEvent (guiActive = true, guiName = "Start Conversion", active = true)]
        public void StartConversion()
        {
            if(!can_convert(true)) return;
            ShuttingOff = false;
            Converting = true;
            next_rate = 1f;
            on_start_conversion();
            update_events();
        }

        [KSPEvent (guiActive = true, guiName = "Stop Conversion", active = true)]
        public void StopConversion()
        { 
            ShuttingOff = true;
            update_events();
        }

        [KSPAction("Toggle Conversion")]
        public void ToggleConversion(KSPActionParam param)
        {
            if(Converting) StopConversion();
            else StartConversion();
        }
        #endregion
    }

    public class ResourceConverterUpdater : ModuleUpdater<AnimatedConverterBase>
    {
        protected override void on_rescale(ModulePair<AnimatedConverterBase> mp, Scale scale)
        { mp.module.SetRatesMultiplier(mp.base_module.RatesMultiplier * scale.absolute.volume); }
    }
}

