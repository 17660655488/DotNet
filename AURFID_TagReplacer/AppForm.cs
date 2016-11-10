using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Symbol.RFID3;
using System.Reflection; 


namespace AURFIDReader
{
    public partial class AppForm : Form
    {
        internal RFIDReader m_ReaderAPI;
        internal bool m_IsConnected;
        internal ConnectionForm m_ConnectionForm;
        internal ReadForm m_ReadForm;
        internal WriteForm m_WriteForm;
        internal LockForm m_LockForm;
        internal KillForm m_KillForm;
        internal BlockEraseForm m_BlockEraseForm;
        internal AccessOperationResult m_AccessOpResult;
        internal TagAccess.WriteSpecificFieldAccessParams overWriteSPParams;
        internal int alertCount = 0;
        internal string m_SelectedTagID = null;
        internal string toReplace = null;
        internal string withReplace = null;
        internal string o_SelectedTagID = null;

        private delegate void UpdateStatus(Events.StatusEventData eventData);
        private UpdateStatus m_UpdateStatusHandler = null;
        private delegate void UpdateRead(Events.ReadEventData eventData);
        private UpdateRead m_UpdateReadHandler = null;
        private TagData m_ReadTag = null;
        private Hashtable m_TagTable;
        private uint m_TagTotalCount;
        
        internal class AccessOperationResult
        {
            public RFIDResults m_Result;
            public ACCESS_OPERATION_CODE m_OpCode;

            public AccessOperationResult()
            {
                m_Result = RFIDResults.RFID_NO_ACCESS_IN_PROGRESS;
                m_OpCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
            }
        }
        public AppForm() 
        {
            /*AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(this.GetType().Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };*/
            InitializeComponent();
            m_ReadTag = new Symbol.RFID3.TagData();
            m_UpdateStatusHandler = new UpdateStatus(myUpdateStatus);
            m_UpdateReadHandler = new UpdateRead(myUpdateRead);           
            m_ConnectionForm = new ConnectionForm(this);
            m_ReadForm = new ReadForm(this);
            m_TagTable = new Hashtable();
            m_AccessOpResult = new AccessOperationResult();
            m_IsConnected = false;
            m_TagTotalCount = 0;
            overWriteSPParams = new TagAccess.WriteSpecificFieldAccessParams();
            overWriteSPParams.WriteDataLength = 12;
            try
            {   m_ConnectionForm.connectionButton_Click(m_ConnectionForm.connectionButton, EventArgs.Empty); }
            catch (Exception e)
            {   functionCallStatusLabel.Text = "Unable to Connect. Check Connections."; }
        }

        private void myUpdateStatus(Events.StatusEventData eventData)
        {
            switch (eventData.StatusEventType)
            {
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.INVENTORY_START_EVENT:
                    functionCallStatusLabel.Text = "Inventory started";
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.INVENTORY_STOP_EVENT:
                    functionCallStatusLabel.Text = "Inventory stopped";
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.ACCESS_START_EVENT:
                    functionCallStatusLabel.Text = "Access Operation started";
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.ACCESS_STOP_EVENT:
                    functionCallStatusLabel.Text = "Access Operation stopped";

                    if (this.m_SelectedTagID == string.Empty)
                    {
                        uint successCount, failureCount;
                        successCount = failureCount = 0;
                        m_ReaderAPI.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);
                        functionCallStatusLabel.Text = "Access completed - Success Count: " + successCount.ToString()
                            + ", Failure Count: " + failureCount.ToString();
                    }
                    resetButtonState();
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.BUFFER_FULL_WARNING_EVENT:
                    functionCallStatusLabel.Text = " Buffer full warning";
                    myUpdateRead(null);
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.BUFFER_FULL_EVENT:
                    functionCallStatusLabel.Text = "Buffer full";
                    myUpdateRead(null);
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.DISCONNECTION_EVENT:
                    functionCallStatusLabel.Text = "Disconnection Event " + eventData.DisconnectionEventData.DisconnectEventInfo.ToString();
                    connectBackgroundWorker.RunWorkerAsync("Disconnect");
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.ANTENNA_EVENT:
                    functionCallStatusLabel.Text = "Antenna Status Update";
                    break;
                case Symbol.RFID3.Events.STATUS_EVENT_TYPE.READER_EXCEPTION_EVENT:
                    functionCallStatusLabel.Text = "Reader ExceptionEvent " + eventData.ReaderExceptionEventData.ReaderExceptionEventInfo;
                    break;
                default:
                    break;
            }
        }

        private void myUpdateRead(Events.ReadEventData eventData)
        {
            int index = 0;
            ListViewItem item;
            
            Symbol.RFID3.TagData[] tagData = m_ReaderAPI.Actions.GetReadTags(1000);
            if (tagData != null)
            {
                for (int nIndex = 0; nIndex < tagData.Length; nIndex++)
                {
                    if (tagData[nIndex].OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_NONE ||
                        (tagData[nIndex].OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ &&
                        tagData[nIndex].OpStatus == ACCESS_OPERATION_STATUS.ACCESS_SUCCESS))
                    {
                        Symbol.RFID3.TagData tag = tagData[nIndex];
                        string tagID = tag.TagID;
                        bool isFound = false;

                        lock (m_TagTable.SyncRoot)
                        {
                            isFound = m_TagTable.ContainsKey(tagID);
                            if (!isFound)
                            {
                                tagID += ((uint)tag.MemoryBank + tag.MemoryBankDataOffset);
                                isFound = m_TagTable.ContainsKey(tagID);
                            }
                        }

                        if (isFound && m_TagTable.Count <3)
                        {
                            uint count = 0;
                            item = (ListViewItem)m_TagTable[tagID];
                            try
                            {
                                count = uint.Parse(item.SubItems[2].Text) + tagData[nIndex].TagSeenCount;
                                m_TagTotalCount += tagData[nIndex].TagSeenCount;
                            }
                            catch (FormatException fe)
                            {
                                functionCallStatusLabel.Text = fe.Message;
                                break;
                            }
                            item.SubItems[1].Text = tag.AntennaID.ToString();
                            item.SubItems[2].Text = count.ToString();
                            item.SubItems[3].Text = tag.PeakRSSI.ToString();

                            string memoryBank = tag.MemoryBank.ToString();
                            index = memoryBank.LastIndexOf('_');
                            if (index != -1)
                            {
                                memoryBank = memoryBank.Substring(index + 1);
                            }
                            if (tag.MemoryBankData.Length > 0 && !memoryBank.Equals(item.SubItems[5].Text))
                            {
                                item.SubItems[5].Text = tag.MemoryBankData;
                                item.SubItems[6].Text = memoryBank;
                                item.SubItems[7].Text = tag.MemoryBankDataOffset.ToString();

                                lock (m_TagTable.SyncRoot)
                                {
                                    m_TagTable.Remove(tagID);
                                    m_TagTable.Add(tag.TagID + tag.MemoryBank.ToString()
                                        + tag.MemoryBankDataOffset.ToString(), item);
                                }
                            }
                            else
                            {
                                item.SubItems[5].Text = DateTime.Now.ToString("yyyy-MM-ddTHH':'mm':'sszzz");
                            }
                        }
                        else if (m_TagTable.Count <3)
                        {
                            item = new ListViewItem(tag.TagID);
                            ListViewItem.ListViewSubItem subItem;

                            subItem = new ListViewItem.ListViewSubItem(item, tag.AntennaID.ToString());
                            item.SubItems.Add(subItem);

                            subItem = new ListViewItem.ListViewSubItem(item, tag.TagSeenCount.ToString());
                            m_TagTotalCount += tag.TagSeenCount;
                            item.SubItems.Add(subItem);

                            subItem = new ListViewItem.ListViewSubItem(item, tag.PeakRSSI.ToString());
                            item.SubItems.Add(subItem);
                            subItem = new ListViewItem.ListViewSubItem(item, tag.PC.ToString("X"));
                            item.SubItems.Add(subItem);

                            subItem = new ListViewItem.ListViewSubItem(item, "");
                            item.SubItems.Add(subItem);
                            subItem = new ListViewItem.ListViewSubItem(item, "");
                            item.SubItems.Add(subItem);
                            subItem = new ListViewItem.ListViewSubItem(item, "");
                            item.SubItems.Add(subItem);

                            inventoryList.BeginUpdate();
                            inventoryList.Items.Add(item);
                            inventoryList.EndUpdate();

                            lock (m_TagTable.SyncRoot)
                            {
                                m_TagTable.Add(tagID, item);
                            }
                        }
                    }
                }
                totalTagValueLabel.Text = m_TagTable.Count + "(" + m_TagTotalCount + ")";
                if (m_TagTable.Count >= 3 || m_TagTotalCount > 50)
                {
                    this.readButton.Text = "Stop";
                    this.readButton_Click(readButton, EventArgs.Empty);
                    if (m_TagTable.Count==2)
                    {
                        //this.readButton.Text = "Stop";
                        //this.readButton_Click(readButton, EventArgs.Empty);
                        foreach (DictionaryEntry entry in m_TagTable)
                        {
                            ListViewItem eachListVw = (ListViewItem)entry.Value;
                            if (eachListVw.Text.StartsWith("30"))
                            {
                                this.withReplace = eachListVw.Text;
                            }
                            else
                            {
                                this.toReplace = eachListVw.Text;
                            }
                            /*foreach (ListViewItem.ListViewSubItem i in eachListVw.SubItems)
                            {
                                confirmationText += i.Text;
                            }*/
                        }
                        if (this.toReplace != null && this.withReplace != null)
                        {
                            confirmToReplace.Text = this.toReplace;
                            confirmWithReplace.Text = this.withReplace;
                        }
                        else if (alertCount<1)
                        {
                            System.Windows.Forms.MessageBox.Show("Overwrite Conflict. Ensure there is only one empty tag and one non-empty tag");
                            alertCount += 1;
                        }
                    }
                    else if (alertCount < 1)
                    {
                        System.Windows.Forms.MessageBox.Show("Overwrite Conflict. Ensure there is only one empty tag and one non-empty tag");
                        alertCount += 1;
                    }
                    //System.Windows.Forms.MessageBox.Show(this, "Overwrite " + this.toReplace + " with " + this.withReplace + ".");
                }
            }
        }            

        private void Events_ReadNotify(object sender, Events.ReadEventArgs readEventArgs)
        {
            try
            {
                this.Invoke(m_UpdateReadHandler, new object[] { readEventArgs.ReadEventData});
            }
            catch (Exception)
            {
               
            }
        }

        public void Events_StatusNotify(object sender, Events.StatusEventArgs statusEventArgs)
        {
            try
            {
                this.Invoke(m_UpdateStatusHandler, new object[] { statusEventArgs.StatusEventData });
            }
            catch (Exception)
            {
            }
        }

        private void accessBackgroundWorker_DoWork(object sender, DoWorkEventArgs accessEvent)
        {
            try
            {
                m_AccessOpResult.m_OpCode = (ACCESS_OPERATION_CODE)accessEvent.Argument;
                m_AccessOpResult.m_Result = RFIDResults.RFID_API_SUCCESS;

                if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ)
                {
                    if (m_SelectedTagID != String.Empty)
                    {
                        m_ReadTag = m_ReaderAPI.Actions.TagAccess.ReadWait(
                        m_SelectedTagID, m_ReadForm.m_ReadParams, null);
                    }
                    else
                    {
                        functionCallStatusLabel.Text = "Enter Tag-Id";
                    }
                }
                else if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_BLOCK_WRITE)
                {
                    
                    if (o_SelectedTagID != String.Empty)
                    {
                        m_ReaderAPI.Actions.TagAccess.WriteTagIDWait(
                            o_SelectedTagID, overWriteSPParams, null);
                    }
                    else
                    {



    
                        
                        
                        
                        
                        functionCallStatusLabel.Text = "Enter Tag-Id";
                    }
                }
                else if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_WRITE)
                {
                    if (m_SelectedTagID != String.Empty)
                    {
                        //m_ReaderAPI.Actions.TagAccess.WriteWait(
                          //  m_SelectedTagID, m_WriteForm.m_WriteParams, null);
                        m_ReaderAPI.Actions.TagAccess.WriteTagIDWait(
                            m_SelectedTagID, m_WriteForm.m_WriteSPParams, null);
                    }
                    else
                    {
                        functionCallStatusLabel.Text = "Enter Tag-Id";
                    }
                    
                }
                else if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_LOCK)
                {
                    if (m_SelectedTagID != String.Empty)
                    {
                        m_ReaderAPI.Actions.TagAccess.LockWait(
                            m_SelectedTagID, m_LockForm.m_LockParams, null);
                    }
                    else
                    {
                        functionCallStatusLabel.Text = "Enter Tag-Id";
                    }
                }
                else if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_KILL)
                {
                    if (m_SelectedTagID != String.Empty)
                    {
                        m_ReaderAPI.Actions.TagAccess.KillWait(
                            m_SelectedTagID, m_KillForm.m_KillParams, null);
                    }
                }
                else if ((ACCESS_OPERATION_CODE)accessEvent.Argument == ACCESS_OPERATION_CODE.ACCESS_OPERATION_BLOCK_ERASE)
                {
                    if (m_SelectedTagID != String.Empty)
                    {
                        m_ReaderAPI.Actions.TagAccess.BlockEraseWait(
                            m_SelectedTagID, m_BlockEraseForm.m_BlockEraseParams, null);
                    }
                    else
                    {
                        functionCallStatusLabel.Text = "Enter Tag-Id";
                    }
                }
            }
            catch (OperationFailureException ofe)
            {
                m_AccessOpResult.m_Result = ofe.Result;
            }
            accessEvent.Result = m_AccessOpResult;
        }

        private void accessBackgroundWorker_ProgressChanged(object sender,
            ProgressChangedEventArgs pce)
        {
         
        }

        private void accessBackgroundWorker_RunWorkerCompleted(object sender, 
            RunWorkerCompletedEventArgs accessEvents)
        {
            int index = 0;
            if (accessEvents.Error != null)
            {
                functionCallStatusLabel.Text = accessEvents.Error.Message;
            }
            else
            {
                // Handle AccessWait Operations              
                AccessOperationResult accessOpResult = (AccessOperationResult) accessEvents.Result;
                if (accessOpResult.m_Result == RFIDResults.RFID_API_SUCCESS)
                {
                    if (accessOpResult.m_OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ)
                    {
                        if (inventoryList.SelectedItems.Count > 0)
                        {
                            ListViewItem item = inventoryList.SelectedItems[0];
                            string tagID = m_ReadTag.TagID + m_ReadTag.MemoryBank.ToString()
                                + m_ReadTag.MemoryBankDataOffset.ToString();

                            if (item.SubItems[5].Text.Length > 0)
                            {
                                bool isFound = false;

                                // Search or add new one
                                lock (m_TagTable.SyncRoot)
                                {
                                    isFound = m_TagTable.ContainsKey(tagID);                                    
                                }
                       
                                if (!isFound)
                                {
                                    ListViewItem newItem = new ListViewItem(m_ReadTag.TagID);
                                    ListViewItem.ListViewSubItem subItem = new ListViewItem.ListViewSubItem(newItem, m_ReadTag.AntennaID.ToString());
                                    newItem.SubItems.Add(subItem);
                                    subItem = new ListViewItem.ListViewSubItem(item, m_ReadTag.TagSeenCount.ToString());
                                    newItem.SubItems.Add(subItem);
                                    subItem = new ListViewItem.ListViewSubItem(item, m_ReadTag.PeakRSSI.ToString());
                                    newItem.SubItems.Add(subItem);
                                    subItem = new ListViewItem.ListViewSubItem(item, m_ReadTag.PC.ToString("X"));
                                    newItem.SubItems.Add(subItem);
                                    subItem = new ListViewItem.ListViewSubItem(item, m_ReadTag.MemoryBankData);
                                    newItem.SubItems.Add(subItem);

                                    string memoryBank = m_ReadTag.MemoryBank.ToString();
                                    index = memoryBank.LastIndexOf('_');
                                    if (index != -1)
                                    {
                                        memoryBank = memoryBank.Substring(index + 1);
                                    }

                                    subItem = new ListViewItem.ListViewSubItem(item, memoryBank);
                                    newItem.SubItems.Add(subItem);
                                    subItem = new ListViewItem.ListViewSubItem(item, m_ReadTag.MemoryBankDataOffset.ToString());
                                    newItem.SubItems.Add(subItem);

                                    inventoryList.BeginUpdate();
                                    inventoryList.Items.Add(newItem);
                                    inventoryList.EndUpdate();

                                    lock (m_TagTable.SyncRoot)
                                    {
                                        m_TagTable.Add(tagID, newItem);
                                    }
                                }                               
                            }
                            else
                            {
                                // Empty Memory Bank Slot
                                item.SubItems[5].Text = m_ReadTag.MemoryBankData;

                                string memoryBank = m_ReadForm.m_ReadParams.MemoryBank.ToString();
                                index = memoryBank.LastIndexOf('_');
                                if (index != -1)
                                {
                                    memoryBank = memoryBank.Substring(index + 1);
                                }
                                item.SubItems[6].Text = memoryBank;
                                item.SubItems[7].Text = m_ReadTag.MemoryBankDataOffset.ToString();

                                lock (m_TagTable.SyncRoot)
                                {
                                    if (m_ReadTag.TagID != null)
                                    {
                                        m_TagTable.Remove(m_ReadTag.TagID);
                                    }
                                    m_TagTable.Add(tagID, item);
                                }
                            }
                            this.m_ReadForm.ReadData_TB.Text = m_ReadTag.MemoryBankData;
                            functionCallStatusLabel.Text = "Read Succeed";
                        }
                    }
                    else if (accessOpResult.m_OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_WRITE)
                    {
                        functionCallStatusLabel.Text = "Write Succeed";
                    }
                    else if (accessOpResult.m_OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_LOCK)
                    {
                        functionCallStatusLabel.Text = "Lock Succeed";
                    }
                    else if (accessOpResult.m_OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_KILL)
                    {
                         functionCallStatusLabel.Text = "Kill Succeed";
                    }
                    else if (accessOpResult.m_OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_BLOCK_ERASE)
                    {
                        functionCallStatusLabel.Text = "BlockErase Succeed";
                    }
                } 
                else
                {
                    functionCallStatusLabel.Text = accessOpResult.m_Result.ToString();
                }
                resetButtonState();
            }
        }

        private void resetButtonState()
        {
            if (m_ReadForm != null)
                m_ReadForm.readButton.Enabled = true;
            if (m_WriteForm != null)
                m_WriteForm.writeButton.Enabled = true;
            if (m_LockForm != null)
                m_LockForm.lockButton.Enabled = true;
            if (m_KillForm != null)
                m_KillForm.killButton.Enabled = true;
            if (m_BlockEraseForm != null)
                m_BlockEraseForm.eraseButton.Enabled = true;
        }

        private void connectBackgroundWorker_DoWork(object sender, DoWorkEventArgs workEventArgs)
        {
            connectBackgroundWorker.ReportProgress(0, workEventArgs.Argument);

            if ((string)workEventArgs.Argument == "Connect")
            {
                m_ReaderAPI = new RFIDReader(m_ConnectionForm.IpText, uint.Parse(m_ConnectionForm.PortText), 0);

                try
                {
                    m_ReaderAPI.Connect();
                    m_IsConnected = true;
                    ushort[] antID = this.m_ReaderAPI.Config.Antennas.AvailableAntennas;
                    ushort antennaID = 0;
                    Antennas.Config antConfig =
                                            this.m_ReaderAPI.Config.Antennas[antID[antennaID]].GetConfig();
                    antConfig.TransmitPowerIndex = 0;
                    this.m_ReaderAPI.Config.Antennas[antID[antennaID]].SetConfig(antConfig);
                    workEventArgs.Result = "Connect Succeed";
               
                }
                catch (OperationFailureException operationException)
                {
                    workEventArgs.Result = operationException.Result;
                }
                catch (Exception ex)
                {
                    workEventArgs.Result = ex.Message;
                }
            }
            else if ((string)workEventArgs.Argument == "Disconnect")
            {
                try
                {
                   
                    m_ReaderAPI.Disconnect();
                    m_IsConnected = false;
                    workEventArgs.Result = "Disconnect Succeed";
                }
                catch (OperationFailureException ofe)
                {
                    workEventArgs.Result = ofe.Result;
                }
            }
          
        }

        private void connectBackgroundWorker_ProgressChanged(object sender,
            ProgressChangedEventArgs progressEventArgs)
        {
            m_ConnectionForm.connectionButton.Enabled = false;
        }

        private void connectBackgroundWorker_RunWorkerCompleted(object sender,
            RunWorkerCompletedEventArgs connectEventArgs)
        {
            if (m_ConnectionForm.connectionButton.Text == "Connect")
            {
                if (connectEventArgs.Result.ToString() == "Connect Succeed")
                {
                    /*
                     *  UI Updates
                     */
                    m_ConnectionForm.connectionButton.Text = "Disconnect";
                    m_ConnectionForm.hostname_TB.Enabled = false;
                    m_ConnectionForm.port_TB.Enabled = false;
                    m_ConnectionForm.transmitPower_CB.Enabled = true;
                    //m_ConnectionForm.Close();
                    this.readButton.Enabled = true;
                    this.readButton.Text = "Read";

                    /*
                     *  Events Registration
                     */
                    m_ReaderAPI.Events.ReadNotify += new Events.ReadNotifyHandler(Events_ReadNotify);
                    m_ReaderAPI.Events.AttachTagDataWithReadEvent = false;
                    m_ReaderAPI.Events.StatusNotify += new Events.StatusNotifyHandler(Events_StatusNotify);
                    m_ReaderAPI.Events.NotifyGPIEvent = true;
                    m_ReaderAPI.Events.NotifyBufferFullEvent = true;
                    m_ReaderAPI.Events.NotifyBufferFullWarningEvent = true;
                    m_ReaderAPI.Events.NotifyReaderDisconnectEvent = true;
                    m_ReaderAPI.Events.NotifyReaderExceptionEvent = true;
                    m_ReaderAPI.Events.NotifyAccessStartEvent = true;
                    m_ReaderAPI.Events.NotifyAccessStopEvent = true;
                    m_ReaderAPI.Events.NotifyInventoryStartEvent = true;
                    m_ReaderAPI.Events.NotifyInventoryStopEvent = true;

                    this.Text = "AURFIDReader Connected to " + m_ConnectionForm.IpText;
                    this.connectionStatus.BackgroundImage =
                        global::AURFIDReader.Properties.Resources.connected;
                }
            }
            else if (m_ConnectionForm.connectionButton.Text == "Disconnect")
            {
                if (connectEventArgs.Result.ToString() == "Disconnect Succeed")
                {
                    this.Text = "AURFIDReader";
                    this.connectionStatus.BackgroundImage =
                        global::AURFIDReader.Properties.Resources.disconnected;

                    m_ConnectionForm.connectionButton.Text = "Connect";
                    m_ConnectionForm.hostname_TB.Enabled = true;
                    m_ConnectionForm.port_TB.Enabled = true;
                    m_ConnectionForm.transmitPower_CB.Enabled = false;
                    this.readButton.Enabled = false;
                    this.readButton.Text = "Read";
                 
                }
            }
            functionCallStatusLabel.Text = connectEventArgs.Result.ToString();
            m_ConnectionForm.connectionButton.Enabled = true;
        }

        private void AppForm_Load(object sender, EventArgs e)
        {
            //connectBackgroundWorker.RunWorkerAsync("Connect");        
        }

        private void AppForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (m_IsConnected)
                {
                    m_ReaderAPI.Disconnect();
                }
                this.Dispose();
            }
            catch (Exception ex)
            {
                functionCallStatusLabel.Text = ex.Message;
            }
        }

        private void connectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_ConnectionForm.ShowDialog(this);
        }

        private void capabilitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CapabilitiesForm capabilitiesForm = new CapabilitiesForm(this);
            capabilitiesForm.ShowDialog(this);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_IsConnected)
            {
                m_ReaderAPI.Disconnect();
            }
            this.Dispose();
        }

        private void readToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                m_ReadForm.ShowDialog(this);            
            }
            catch (Exception ex)
            {
                functionCallStatusLabel.Text = "Read Form:" + ex.Message;
            }
        }

        private void writeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_WriteForm)
            {
                m_WriteForm = new WriteForm(this, false);
            }
            m_WriteForm.ShowDialog(this);
        }

        private void lockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_LockForm)
            {
                m_LockForm = new LockForm(this);
            }
            m_LockForm.ShowDialog(this);
        }

        private void killToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_KillForm)
            {
                m_KillForm = new KillForm(this);
            }
            m_KillForm.ShowDialog(this);
        }

        private void blockWriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_WriteForm)
            {
                m_WriteForm = new WriteForm(this, true);
            }
            m_WriteForm.ShowDialog(this);
        }

        private void blockEraseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_BlockEraseForm)
            {
                m_BlockEraseForm = new BlockEraseForm(this);
            }
            m_BlockEraseForm.ShowDialog(this);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HelpForm helpDialog = new HelpForm(this);
            if (helpDialog.ShowDialog(this) == DialogResult.Yes)
            {

            }
            helpDialog.Dispose();
        }

        private void readButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (m_IsConnected)
                {
                    if (readButton.Text == "Read")
                    {
                        m_ReaderAPI.Actions.Inventory.Perform(null, null, null);

                        inventoryList.Items.Clear();
                        m_TagTable.Clear();
                        this.alertCount = 0;
                        m_TagTotalCount = 0;

                        readButton.Text = "Stop";
                    }
                    else if (readButton.Text == "Stop")
                    {
                        if (m_ReaderAPI.Actions.TagAccess.OperationSequence.Length > 0)
                        {
                            m_ReaderAPI.Actions.TagAccess.OperationSequence.StopSequence();
                        }
                        else
                        {
                            m_ReaderAPI.Actions.Inventory.Stop();
                        }

                        readButton.Text = "Read";
                    }                    
                }
                else
                {
                    functionCallStatusLabel.Text = "Please connect to a reader";
                }
            }
            catch (InvalidOperationException ioe)
            {
                functionCallStatusLabel.Text = ioe.Message;
            }
            catch (InvalidUsageException iue)
            {
                functionCallStatusLabel.Text = iue.Info;
            }
            catch (OperationFailureException ofe)
            {
                functionCallStatusLabel.Text = ofe.Result + ":" + ofe.StatusDescription;
            }
            catch (Exception ex)
            {
                functionCallStatusLabel.Text = ex.Message;
            }
        }

        void inventoryList_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dataContextMenuStrip.Show(inventoryList, new Point(e.X, e.Y));
            }
        }

        private void tagDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagDataForm tagDataForm = new TagDataForm(this);
            tagDataForm.ShowDialog(this);
        }

        private void readDataContextMenuItem_Click(object sender, EventArgs e)
        {
            m_ReadForm.ShowDialog(this);
        }

        private void writeDataContextMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_WriteForm)
            {
                m_WriteForm = new WriteForm(this, false);
            }
            m_WriteForm.ShowDialog(this);
        }

        private void lockDataContextMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_LockForm)
            {
                m_LockForm = new LockForm(this);
            }
            m_LockForm.ShowDialog(this);
        }

        private void killDataContextMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_KillForm)
            {
                m_KillForm = new KillForm(this);
            }
            m_KillForm.ShowDialog(this);
        }

        private void blockWriteDataContextMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_WriteForm)
            {
                m_WriteForm = new WriteForm(this, true);
            }
            m_WriteForm.ShowDialog(this);
        }

        private void blockEraseDataContextMenuItem_Click(object sender, EventArgs e)
        {
            if (null == m_BlockEraseForm)
            {
                m_BlockEraseForm = new BlockEraseForm(this);
            }
            m_BlockEraseForm.ShowDialog(this);
        }

        private void resetToFactoryDefaultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (m_ReaderAPI.IsConnected)
                {
                    m_ReaderAPI.Config.ResetFactoryDefaults();
                }
            }
            catch (Exception ex)
            {
                functionCallStatusLabel.Text = ex.Message;
            }
        }

        private void clearReportsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.inventoryList.Items.Clear();
            this.m_TagTable.Clear();
        }

        private void clearReports_CB_CheckedChanged(object sender, EventArgs e)
        {
            this.totalTagValueLabel.Text = "0(0)";
            this.inventoryList.Items.Clear();
            this.m_TagTable.Clear();
            clearReports_CB.Checked = false;
        }

        private void AppForm_ClientSizeChanged(object sender, EventArgs e)
        {
            functionCallStatusLabel.Width = this.Width - 77;
        }

        private void inventoryList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void connectHome_Click(object sender, EventArgs e)
        {
            try
            {
                connectBackgroundWorker.RunWorkerAsync(connectHome.Text);
                if (connectHome.Text == "Connect")
                {
                    connectHome.Text = "Disconnect";
                }
                else if (connectHome.Text == "Disconnect")
                {
                    connectHome.Text = "Connect";
                }
            }
            catch (Exception ex)
            {
                functionCallStatusLabel.Text = ex.Message;
            }
            
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void confirm_Click(object sender, EventArgs e)
        {
            int emptyTag = 0;
            if (emptyTag != 0)
            {
                o_SelectedTagID = confirmWithReplace.Text;
                string emptyTagText = "E28011606000020507A5EEEE";
                overWriteSPParams.WriteDataLength = (uint)(emptyTagText.Length / 2);
                if (overWriteSPParams.WriteDataLength % 2 != 0)
                {
                    overWriteSPParams.WriteDataLength -= 1;
                }
                byte[] emptyData = new byte[overWriteSPParams.WriteDataLength];
                for (int index = 0; index < overWriteSPParams.WriteDataLength; index += 2)
                {
                    emptyData[index] = byte.Parse(emptyTagText.Substring(index * 2, 2),
                        System.Globalization.NumberStyles.HexNumber);
                    emptyData[index + 1] = byte.Parse(emptyTagText.Substring((index + 1) * 2, 2),
                        System.Globalization.NumberStyles.HexNumber);
                }

                overWriteSPParams.WriteData = emptyData;
                this.accessBackgroundWorker.RunWorkerAsync(ACCESS_OPERATION_CODE.ACCESS_OPERATION_WRITE);
                Thread.Sleep(50);
            }
            o_SelectedTagID = confirmToReplace.Text;
            overWriteSPParams.WriteDataLength = (uint)(this.confirmWithReplace.Text.Length /2);
            if (overWriteSPParams.WriteDataLength % 2 != 0)
            {
                overWriteSPParams.WriteDataLength -= 1;
            }
            byte[] writeData = new byte[overWriteSPParams.WriteDataLength];
            for (int index = 0; index < overWriteSPParams.WriteDataLength; index += 2)
            {
                writeData[index] = byte.Parse(this.confirmWithReplace.Text.Substring(index * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
                writeData[index + 1] = byte.Parse(this.confirmWithReplace.Text.Substring((index + 1) * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
            }
            overWriteSPParams.WriteData = writeData;
            this.accessBackgroundWorker.RunWorkerAsync(ACCESS_OPERATION_CODE.ACCESS_OPERATION_BLOCK_WRITE);
            this.confirmWithReplace.Text = "";
            this.confirmToReplace.Text = "";
        }

        private void denyOverwrite_Click(object sender, EventArgs e)
        {
            this.confirmWithReplace.Text = "";
            this.confirmToReplace.Text = "";
        }
    }
}