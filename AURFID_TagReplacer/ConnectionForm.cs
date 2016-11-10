using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Symbol.RFID3;

namespace AURFIDReader
{
    public partial class ConnectionForm : Form
    {
        private AppForm m_AppForm;

        public ConnectionForm(AppForm appForm)
        {
            InitializeComponent();
            m_AppForm = appForm;
        }

        public string IpText
        {
            get
            {
                return hostname_TB.Text;
            }
        }

        public string PortText
        {
            get
            {
                return port_TB.Text;
            }
        }

        public void connectionButton_Click(object sender, EventArgs e)
        {
            try
            {   m_AppForm.connectBackgroundWorker.RunWorkerAsync(connectionButton.Text);
            if (m_AppForm.connectHome.Text == "Connect")
            {
                m_AppForm.connectHome.Text = "Disconnect";
            }
            else if (m_AppForm.connectHome.Text == "Disconnect")
            {
                m_AppForm.connectHome.Text = "Connect";
            }
            }
            catch (Exception ex)
            {
                m_AppForm.functionCallStatusLabel.Text = ex.Message;
            }
        }

        private void ConnectionForm_Load(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void transmitPower_CB_SelectedIndexChanged(object sender, EventArgs e)
        {
            //int[] txValues = this.m_ReaderAPI.ReaderCapabilities.TransmitPowerLevelValues;
            ushort[] antID = m_AppForm.m_ReaderAPI.Config.Antennas.AvailableAntennas;
            ushort antennaID = 0;
            Antennas.Config antConfig =
                                    m_AppForm.m_ReaderAPI.Config.Antennas[antID[antennaID]].GetConfig();
            antConfig.TransmitPowerIndex = (ushort)this.transmitPower_CB.SelectedIndex;
            m_AppForm.m_ReaderAPI.Config.Antennas[antID[antennaID]].SetConfig(antConfig);
                
        }
    }
}