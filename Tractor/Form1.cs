﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Com.QuantAsylum.Tractor.Settings;
using Com.QuantAsylum.Tractor.TestManagers;
using Com.QuantAsylum.Tractor.Tests;
using Com.QuantAsylum.Tractor.Tests.GainTests;
using Com.QuantAsylum.Tractor.Ui.Extensions;
using Com.QuantAsylum.Tractor.Dialogs;
using Tractor.Com.QuantAsylum.Tractor.Dialogs;
using System.Threading;
using Tractor.Com.QuantAsylum.Tractor.Tests;

namespace Tractor
{
    public partial class Form1 : Form
    {
        internal static Form1 This;

        TestManager Tm;
        bool AppSettingsDirty = false;

        static internal string SettingsFile = "";

        bool HasRun = false;

        TestBase SelectedTb;

        ObjectEditor ObjEditor;
        bool EditingInProgress = false;

        static internal AppSettings AppSettings;

        public Form1()
        {
            This = this;
            InitializeComponent();

#if !DEBUG
            DlgSplash splash = new DlgSplash();
            splash.Show();

            Thread.Sleep(1000);
#endif

            AppSettings = new AppSettings();
            label3.Text = "";
            Tm = new TestManager();
            Type t = Type.GetType(AppSettings.TestClass);
            Tm.TestClass = Activator.CreateInstance(t);
        }

        /// <summary>
        /// Called when user begins editing a test
        /// </summary>
        internal void StartEditing()
        {
            RunTestsBtn.Enabled = false;
            MoveUpBtn.Enabled = false;
            MoveDownBtn.Enabled = false;
            DeleteBtn.Enabled = false;
            treeView1.Enabled = false;
            menuStrip1.Enabled = false;
            AddTestBtn.Enabled = false;
        }

        /// <summary>
        /// Called when user finishes editing a test
        /// </summary>
        internal void DoneEditing()
        {
            treeView1.Enabled = true;
            menuStrip1.Enabled = true;
            AddTestBtn.Enabled = true;
            AppSettingsDirty = true;
            RePopulateTreeView();
            SetTreeviewControls();
            UpdateTestConcerns(SelectedTb);
        }

        /// <summary>
        /// Called when user cancels editing a test
        /// </summary>
        internal void CancelEditing()
        {
            treeView1.Enabled = true;
            menuStrip1.Enabled = true;
            AddTestBtn.Enabled = true;
            RePopulateTreeView();
            SetTreeviewControls();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Directory.CreateDirectory(Constants.DataFilePath);
            Directory.CreateDirectory(Constants.TestLogsPath);
            Directory.CreateDirectory(Constants.AuditPath);
            Directory.CreateDirectory(Constants.PidPath);

            Text = Constants.TitleBarText + " " + Constants.Version.ToString("0.00") + Constants.VersionSuffix;

            DefaultTreeview();

            SetTreeviewControls();

            Com.QuantAsylum.Tractor.Database.AuditDb.StartBackgroundWorker();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If we have run anything, make sure power to the DUT is off. 
            if (HasRun)
            {
                try
                {
                    ((IPowerSupply)Tm.TestClass).SetSupplyState(false);
                }
                catch
                {

                }
            }

            // Here, the data is clean. Check if we need to save the current TestManager data
            if (AppSettingsDirty)
            {
                if (MessageBox.Show("Do you want to save the current test plan?", "Changes not saved", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    saveTestPlanToolStripMenuItem_Click(null, null);
                }
            }
        }

        /// <summary>
        /// Sets treeview defaults
        /// </summary>
        private void DefaultTreeview()
        {
            treeView1.CheckBoxes = true;
            treeView1.HideSelection = false;
        }

        /// <summary>
        /// Adds a node to the treeview
        /// </summary>
        /// <param name="testName"></param>
        /// <param name="test"></param>
        private void TreeViewAdd(string testName, TestBase test)
        {
            TreeNode root = new TreeNode();
            root.Text = testName + "   [" + (test as TestBase).GetTestName() + "]";

            if ((test as AudioTestBase).RunTest)
                root.Checked = true;

            treeView1.Nodes.Add(root);
        }

        /// <summary>
        /// Populates the treeview based on the data in the TestManager. This tries to keep the current node
        /// selected unless another ID to highlight is presented
        /// </summary>
        internal void RePopulateTreeView(string highlightId = "")
        {
            if ((highlightId == "") && (treeView1.SelectedNode != null))
            {
                var tn = treeView1.Nodes.Cast<TreeNode>().First(o => o.Text.Contains(treeView1.SelectedNode.Text));
                highlightId = tn.Text;
            }

            treeView1.Nodes.Clear();

            for (int i = 0; i < AppSettings.TestList.Count(); i++)
            {
                TreeViewAdd((AppSettings.TestList[i] as TestBase).Name, AppSettings.TestList[i]);
            }

            treeView1.ExpandAll();

            if (highlightId != "")
            {
                TreeNode[] tn = treeView1.Nodes.Cast<TreeNode>().Where(o => o.Text.Contains(highlightId)).ToArray();

                if (tn.Length > 0)
                    treeView1.SelectedNode = tn[0];
            }
            else
            {
                if (treeView1.Nodes.Count > 0)
                    treeView1.SelectedNode = treeView1.Nodes[0];
            }
        }

        /// <summary>
        /// Given a string such as "abctest0 [xyz]" this returns abctest0
        /// </summary>
        /// <param name="testString"></param>
        /// <returns></returns>
        private string GetTestName(string testString)
        {
            string[] toks = testString.Split('[', ']');

            return toks[0].Trim();
        }

        /// <summary>
        /// Given a string such as "abctest0 [xyz]" this returns xyz
        /// </summary>
        /// <param name="testString"></param>
        /// <returns></returns>
        private string GetTestClass(string testString)
        {
            string[] toks = testString.Split('[', ']');

            return toks[1].Trim();
        }

        /// <summary>
        /// Called after a node has been selected. This will update the UI in the 
        /// righthand panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            ClearEditFields();

            if (e.Node.Level != 0)
                return;

            ClearEditFields();
            TestBase tb = AppSettings.TestList.Find(o => o.Name == GetTestName(e.Node.Text));
            SelectedTb = tb;
            ObjEditor = new ObjectEditor(this, SelectedTb, tableLayoutPanel1, NowEditingCallback);

            UpdateTestConcerns(tb);

            SetTreeviewControls();
        }

        private void NowEditingCallback()
        {
            treeView1.Enabled = false;
            EditingInProgress = true;
            SetTreeviewControls();
        }

        private void UpdateTestConcerns(TestBase tb)
        {
            string s;

            if (tb == null)
            {
                s = "Description: \n\nRunnable: \n\nIssues:";
            }
            else if (tb.IsRunnable())
            {
                tb.CheckValues(out string values);
                s = string.Format("Description: {0}\n\nRunnable: Yes\n\nIssues:{1}", tb.GetTestDescription(), values == "" ? "None" : values);
            }
            else
            {
                s = string.Format("Description: {0}\n\nRunnable: No. The selected test class does not support this test.", tb.GetTestDescription());

            }
            label3.Text = s;
        }

        /// <summary>
        /// Called after a checkbox in the treeview has been checked (or unchecked)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            TestBase tb = AppSettings.TestList.Find(o => o.Name == GetTestName(e.Node.Text));
            SelectedTb = tb;
            tb.RunTest = e.Node.Checked;
        }

        /// <summary>
        /// Sets button on/off states depending on treeview state
        /// </summary>
        private void SetTreeviewControls()
        {
            if (EditingInProgress)
            {
                treeView1.Enabled = false;
                panel4.Enabled = false;
                menuStrip1.Enabled = false;
            }
            else
            {
                treeView1.Enabled = true;
                panel4.Enabled = true;
                menuStrip1.Enabled = true;
            }

            if (treeView1.Nodes.Count == 0)
                RunTestsBtn.Enabled = false;
            else
                RunTestsBtn.Enabled = true;

            if (treeView1.SelectedNode == null)
            {
                MoveUpBtn.Enabled = false;
                MoveDownBtn.Enabled = false;
                DeleteBtn.Enabled = false;
                return;
            }

            if (treeView1.SelectedNode != null)
                DeleteBtn.Enabled = true;

            if (treeView1.SelectedNode.Index == 0)
                MoveUpBtn.Enabled = false;
            else
                MoveUpBtn.Enabled = true;

            if (treeView1.SelectedNode.Index == treeView1.Nodes.Count - 1)
            {
                MoveDownBtn.Enabled = false;
            }
            else
            {
                MoveDownBtn.Enabled = true;
            }
        }

        /// <summary>
        /// Add Test
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddButton_Click(object sender, EventArgs e)
        {
            DlgAddTest dlg = new DlgAddTest(Tm);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string className = dlg.GetSelectedTestName();

                TestBase testInst = CreateTestInstance(className);
                testInst.Tm = Tm;
                (testInst as TestBase).Name = dlg.textBox1.Text;
                AppSettings.TestList.Add(testInst as TestBase);

                TreeViewAdd(dlg.textBox1.Text, CreateTestInstance(className));
                AppSettingsDirty = true;

                SelectedTb = AppSettings.TestList.Last();
                RePopulateTreeView(SelectedTb.Name);
                UpdateTestConcerns(SelectedTb);
            }

            SetTreeviewControls();
        }

        /// <summary>
        /// Creates an instance based on the classname. This is used when the user
        /// specifies a particular test they'd like to run. That test name is mapped
        /// to a class, and then an instance of that class is created
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        private TestBase CreateTestInstance(string className)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var type = assembly.GetTypes().First(t => t.Name == className);

            return (TestBase)Activator.CreateInstance(type);
        }

        private void ClearEditFields()
        {
            for (int i = tableLayoutPanel1.Controls.Count - 1; i >= 0; --i)
                tableLayoutPanel1.Controls[i].Dispose();

            tableLayoutPanel1.Controls.Clear();
            tableLayoutPanel1.RowCount = 0;
        }

        /// <summary>
        /// Pops a modal dlg where the user is given a chance to review the tests and then execute them
        /// over and over if desired. This doesn't return until the user closes the dlg
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunTestBtn_Click(object sender, EventArgs e)
        {
            if (AppSettings.TestList.Count == 0)
                return;

            // Make sure all tests are runnable
            foreach (TestBase tb in AppSettings.TestList)
            {
                if (tb.IsRunnable() == false)
                {
                    MessageBox.Show("Not all tests are runnable. Make sure you have the correct class specified in Settings");
                    return;
                }
            }

            DlgTestRun dlg = new DlgTestRun(Tm, TestRunCallback, Constants.TestLogsPath);

            this.Visible = false;
            HasRun = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {

            }
            else
            {

            }
            this.Visible = true;
        }
      
        // Called when current tests are done running. Right now, we don't use this.
        public void TestRunCallback()
        {

        }


        /// <summary>
        /// Deletes the currently selected test
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteBtn_Click(object sender, EventArgs e)
        {
            AppSettings.TestList.RemoveAt(treeView1.SelectedNode.Index);
            AppSettingsDirty = true;
            RePopulateTreeView();

            SetTreeviewControls();
        }

        private void newTestPlan_Click(object sender, EventArgs e)
        {
            if (AppSettingsDirty)
            {
                if (MessageBox.Show("You have unsaved data. Save it first?", "Unsaved data", MessageBoxButtons.YesNo) == DialogResult.OK)
                {
                    if (SettingsFile != "")
                        saveTestPlanToolStripMenuItem_Click(null, null);
                    else
                        saveAsToolStripMenuItem_Click(null, null);
                }
            }

            AppSettings = new AppSettings();
            AppSettingsDirty = true;
            Type t = Type.GetType(AppSettings.TestClass);
            Tm.TestClass = Activator.CreateInstance(t);
            foreach (TestBase test in AppSettings.TestList)
            {
                test.SetTestManager(Tm);
            }
            ClearEditFields();
            UpdateTestConcerns(null);
            RePopulateTreeView();
        }

        /// <summary>
        /// Loads settings from file system
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void loadTestPlanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AppSettingsDirty)
            {
                if (MessageBox.Show("You have unsaved data. Save it first?", "Unsaved data", MessageBoxButtons.YesNo) == DialogResult.OK)
                {
                    if (SettingsFile != "")
                        saveTestPlanToolStripMenuItem_Click(null, null);
                    else
                        saveAsToolStripMenuItem_Click(null, null);
                }
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Constants.DataFilePath;
            ofd.Filter = "Test Profile files (*.tp)|*.tp|All files (*.*)|*.*";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                SettingsFile = ofd.FileName;
                AppSettings = AppSettings.Deserialize(File.ReadAllText(ofd.FileName));
                Type t = Type.GetType(AppSettings.TestClass);
                Tm.TestClass = Activator.CreateInstance(t);
                AppSettingsDirty = false;

                foreach (TestBase test in AppSettings.TestList)
                {
                    test.SetTestManager(Tm);
                }
                RePopulateTreeView();
            }
        }

        /// <summary>
        /// Saves settings to file system
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveTestPlanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SettingsFile == "")
                return;

            File.WriteAllText(SettingsFile, AppSettings.Serialize());
            AppSettingsDirty = false;
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.InitialDirectory = Constants.DataFilePath;
            sfd.Filter = "Test Profile files (*.tp)|*.tp|All files (*.*)|*.*";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                SettingsFile = sfd.FileName;
                File.WriteAllText(SettingsFile, AppSettings.Serialize());
                AppSettingsDirty = false;
            }
        }

        /// <summary>
        /// Moves a test up in the treeview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveUpBtn_Click(object sender, EventArgs e)
        {
            if (SelectedTb != null)
            {
                int index = AppSettings.TestList.IndexOf(SelectedTb);

                if (index == 0)
                    return;

                AppSettings.TestList.RemoveAt(index);
                AppSettings.TestList.Insert(index - 1, SelectedTb);
                AppSettingsDirty = true;
                RePopulateTreeView(SelectedTb.Name);
            }
        }

        /// <summary>
        /// Moves a test down in the treeview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveDownBtn_Click(object sender, EventArgs e)
        {
            if (SelectedTb != null)
            {
                int index = AppSettings.TestList.IndexOf(SelectedTb);

                if (index == AppSettings.TestList.Count - 1)
                    return;

                AppSettings.TestList.RemoveAt(index);
                AppSettings.TestList.Insert(index + 1, SelectedTb);
                AppSettingsDirty = true;
                RePopulateTreeView(SelectedTb.Name);
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DlgSettings dlg = new DlgSettings(AppSettings);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                // BUGBUG! If we're already connected via Dotnet remoting, then we first need to 
                // close the connection before we try to create the instance again. Otherwise
                // we leave the socket hanging. Since the QA401 is the only device using remoting
                // we'll handle it special here. Long term, we move away from this and this can
                // be removed. When that day does come, the CloseConnection() method should 
                // be pulled from all interfaces as it's no longer needed.
                if (Tm.TestClass is IInstrument)
                {
                    ((IInstrument)Tm.TestClass).CloseConnection();
                }

                Type t = Type.GetType(AppSettings.TestClass);
                Tm.TestClass = Activator.CreateInstance(t);
            }
        } 

        private void openLogInBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string s = Constants.TestLogsPath + Constants.LogFileName;

            if (File.Exists(s))
            {
                System.Diagnostics.Process.Start(s);
            }
            else
            {
                MessageBox.Show("The log doesn't yet exist.");
            }
        }

        private void queryCloudToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DlgQuery dlg = new DlgQuery();

            if (dlg.ShowDialog() == DialogResult.OK)
            {

            }

        }

        public void AcceptChanges()
        {
            AppSettingsDirty = true;
            EditingInProgress = false;
            treeView1.Enabled = true;
            SetTreeviewControls();
        }

        public void AbandonChanges()
        {
            EditingInProgress = false;
            treeView1.Enabled = true;
            SetTreeviewControls();
        }
    }
}
