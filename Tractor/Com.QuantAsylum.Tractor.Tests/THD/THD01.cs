﻿using Com.QuantAsylum;
using Com.QuantAsylum.Tractor.TestManagers;
using Com.QuantAsylum.Tractor.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tractor.Com.QuantAsylum.Tractor.TestManagers;

namespace Tractor.Com.QuantAsylum.Tractor.Tests.THDs
{
    [Serializable]
    public class Thd01 : TestBase
    {
        public float Freq = 1000;
        public float OutputLevel = -30;

        public float MinimumOKTHD = -110;
        public float MaximumOKTHD = -100;

        public int InputRange = 6;

        public Thd01() : base()
        {
            Name = "THD01";
            TestType = TestTypeEnum.Distortion;
        }

        public override void DoTest(string title, out TestResult tr)
        {
            // Two channels of testing
            tr = new TestResult(2);

            Tm.SetInstrumentsToDefault();
            Tm.AudioAnalyzerSetTitle(title);
            Tm.SetInputRange(InputRange);

            Tm.AudioGenSetGen1(true, OutputLevel, Freq);
            Tm.AudioGenSetGen2(false, OutputLevel, Freq);
            Tm.RunSingle();

            while (Tm.AnalyzerIsBusy())
            {

            }

            TestResultBitmap = Tm.GetBitmap();

            tr.Value[0] = (float)Tm.ComputeThdPct(Tm.GetData(ChannelEnum.Left), Freq, 20000);
            tr.Value[1] = (float)Tm.ComputeThdPct(Tm.GetData(ChannelEnum.Right), Freq, 20000);

            // Convert to db
            tr.Value[0] = 20 * (float)Math.Log10(tr.Value[0] / 100);
            tr.Value[1] = 20 * (float)Math.Log10(tr.Value[1] / 100);

            bool passLeft = true, passRight = true;

            if (LeftChannel)
            {
                tr.StringValue[0] = tr.Value[0].ToString("0.0") + " dB";
                if ((tr.Value[0] < MinimumOKTHD) || (tr.Value[0] > MaximumOKTHD))
                    passLeft = false;
            }
            else
                tr.StringValue[0] = "SKIP";

            if (RightChannel)
            {
                tr.StringValue[1] = tr.Value[1].ToString("0.0") + " dB";
                if ((tr.Value[1] < MinimumOKTHD) || (tr.Value[1] > MaximumOKTHD))
                    passLeft = false;
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

        public override string GetTestDescription()
        {
            return "Measures THD at a given frequency and amplitude. Results must be within a given window to 'pass'.";
        }
    }
}
