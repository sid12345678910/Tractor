﻿using Com.QuantAsylum.Tractor.TestManagers;
using Com.QuantAsylum.Tractor.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tractor.Com.QuantAsylum.Tractor.TestManagers;

namespace Tractor.Com.QuantAsylum.Tractor.Tests.Other
{
    /// <summary>
    /// This test will check the gain at a given impedance level
    /// </summary>
    [Serializable]
    public class Impedance01 : TestBase
    {           
        public float Freq = 1000;
        public float OutputLevel = -30;

        public int InputRange = 6;

        public float MinimumPassImpedance = 0.01f;
        public float MaximumPassImpedance = 0.2f;

        public Impedance01() : base()
        {
            TestType = TestTypeEnum.Other;
        }

        public override void DoTest(string title, out TestResult tr)
        {
            // Two channels of testing
            tr = new TestResult(2);

            float[] vOut4 = new float[2] { float.NaN, float.NaN };
            float[] vOut8 = new float[2] { float.NaN, float.NaN };

            Tm.SetInstrumentsToDefault();
            Tm.AudioAnalyzerSetTitle(title);
            Tm.SetInputRange(InputRange);

            // First, we make 8 ohm measurement
            Tm.LoadSetImpedance(8); Thread.Sleep(Constants.QA450RelaySettle);
            Tm.AudioGenSetGen1(true, OutputLevel, Freq);
            Tm.AudioGenSetGen2(false, OutputLevel, Freq);
            Tm.RunSingle();

            while (Tm.AnalyzerIsBusy())
            {
                Thread.Sleep(30);
            }

            // Grab the 8 ohm levels
            if (LeftChannel)
                vOut8[0] = (float)Tm.ComputeRms(Tm.GetData(ChannelEnum.Left), Freq * 0.98f, Freq * 1.02f );

            if (RightChannel)
                vOut8[1] = (float)Tm.ComputeRms(Tm.GetData(ChannelEnum.Right), Freq * 0.98f, Freq * 1.02f);

            // Now make 4 ohm meausrement
            Tm.LoadSetImpedance(4); Thread.Sleep(Constants.QA450RelaySettle);

            Tm.RunSingle();

            while (Tm.AnalyzerIsBusy())
            {
                Thread.Sleep(30);
            }

            // Grab the 4 ohm circuit levels
            vOut4[0] = (float)Tm.ComputeRms(Tm.GetData(ChannelEnum.Left), Freq * 0.98f, Freq * 1.02f);
            vOut4[1] = (float)Tm.ComputeRms(Tm.GetData(ChannelEnum.Right), Freq * 0.98f, Freq * 1.02f);

            // Compute impedance
            for (int i = 0; i < 2; i++)
            {
                if (!float.IsNaN(vOut4[i]) && !float.IsNaN(vOut8[i]))
                    tr.Value[i] = CalcImpedance(vOut4[i], vOut8[i]);
            }

            bool passLeft = true, passRight = true;

            if (LeftChannel)
            {
                tr.StringValue[0] = tr.Value[0].ToString("0.0000") + " ohms";
                if ((tr.Value[0] < MinimumPassImpedance) || (tr.Value[0] > MaximumPassImpedance))
                    passLeft = false;
            }
            else
                tr.StringValue[0] = "SKIP";

            if (RightChannel)
            {
                tr.StringValue[1] = tr.Value[1].ToString("0.0000") + " ohms";
                if ((tr.Value[1] < MinimumPassImpedance) || (tr.Value[1] > MaximumPassImpedance))
                    passRight = false;
            }
            else
                tr.StringValue[1] = "SKIP";


            if (LeftChannel && RightChannel)
                tr.Pass = passLeft && passRight;
            else if (LeftChannel)
                tr.Pass = passLeft;
            else if (RightChannel)
                tr.Pass = passRight;

            return;
        }

        /// <summary>
        /// Calculates the output impedance given two measurements: Amplitude at 4 ohms and amplitude at 8 ohms.
        /// We have two equations and two unknowns:
        /// Vout4 = Vin * 4/(R+4)
        /// Vout8 = Vin * 8/(R+8)
        /// Vin is the same for both, and R is the same for both
        /// Vin is the input of the amp BEFORE the amp's output impedance
        /// R is the amp output impedance
        /// To solve, re-arrange eq 1 in terms of Vin = Vout1 * (R+4)/4
        /// Then sub that into equation 2 for Vin. And then go to Wolfram and
        /// ask wolfram to solve for R. That yields R = 8*(Vout4 - Vout8)/(2*Vout4 - Vout8)
        /// </summary>
        /// <param name="openCircuitLevel"></param>
        /// <param name="loadedCircuitLevel"></param>
        /// <param name="impedance"></param>
        /// <returns></returns>
        float CalcImpedance(float vOut4, float vOut8)
        {
            // Convert levels to linear
            vOut4 = (float)Math.Pow(10, vOut4 / 20);
            vOut8 = (float)Math.Pow(10, vOut8 / 20);
            return - 8 * (vOut4 - vOut8)/(2*vOut4 - vOut8);
        }

        public override bool CheckValues(out string s)
        {
            s = "";

            if (Tm.GetInputRanges().Contains(InputRange) == false)
            {
                s = "Input range not supported. Must be: " + string.Join(" ", Tm.GetInputRanges());
                return false;
            }

            return true;
        }

        public override string GetTestLimitsString()
        {
            return string.Format("{0:N1}...{1:N1} Ohms", MinimumPassImpedance, MaximumPassImpedance);
        }

        public override string GetTestDescription()
        {
            return "Measures the output impedance of an amplifier using an " +
                   "open-circuit measurement and a measurement at a " +
                   "specified load.";
        }
    }
}