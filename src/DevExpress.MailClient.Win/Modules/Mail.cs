﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraGrid.Controls;
using DevExpress.Utils.Design;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;

namespace DevExpress.MailClient.Win {
    public partial class Mail : BaseModule {
        Message currentMessage;
        PopupMenu priorityMenu, dateFilterMenu;
        RibbonControl ribbon;
        FindControlManager findControlManager = null;
        FilterCriteriaManager filterCriteriaManager = null;
        Timer messageReadTimer;
        int focusedRowHandle = 0;
        bool lockUpdateCurrentMessage = true;
        public override string ModuleName { get { return Properties.Resources.MailName; } }
        public Mail() {
            InitializeComponent();
            CreateTimer();
        }
        void CreateTimer() {
            messageReadTimer = new Timer();
            messageReadTimer.Interval = 3000;
            messageReadTimer.Tick += new EventHandler(messageReadTimer_Tick);
        }

        void messageReadTimer_Tick(object sender, EventArgs e) {
            if(CurrentMessage != null && CurrentMessage.IsUnread) {
                RaiseReadMessagesChanged(gridView1.FocusedRowHandle);
                messageReadTimer.Stop();
            }
        }
        protected internal override RichEditControl CurrentRichEdit { get { return ucMailViewer1.RichEdit; } }
        protected override DevExpress.XtraGrid.GridControl Grid { get { return gridControl1; } }
        internal override void InitModule(DevExpress.Utils.Menu.IDXMenuManager manager, object data) {
            base.InitModule(manager, data);
            EditorHelper.InitPriorityComboBox(repositoryItemImageComboBox1);
            this.ribbon = manager as RibbonControl;
            ucMailViewer1.SetMenuManager(manager);
            ShowAboutRow();
        }
        void ShowAboutRow() {
            Timer tmr = new Timer();
            tmr.Interval = 100;
            tmr.Tick += new EventHandler(tmr_Tick);
            tmr.Start();
        }
        void tmr_Tick(object sender, EventArgs e) {
            lockUpdateCurrentMessage = false;
            FocusRow(0);
            ((Timer)sender).Stop();
        }
        void FocusRow(int rowHandle) {
            gridView1.FocusedRowHandle = rowHandle;
            gridView1.ClearSelection();
            gridView1.SelectRow(rowHandle);
        }
        internal override void ShowModule(bool firstShow) {
            base.ShowModule(firstShow);
            if(firstShow) {
                filterCriteriaManager = new FilterCriteriaManager(gridView1);
                filterCriteriaManager.AddBarItem(OwnerForm.ShowUnreadItem, gcIcon, "[Read] = 0");
                filterCriteriaManager.AddBarItem(OwnerForm.ImportantItem, gcPriority, "[Priority] = 2");
                filterCriteriaManager.AddBarItem(OwnerForm.HasAttachmentItem, gcAttachment, "[Attachment] = 1");
                filterCriteriaManager.AddClearFilterButton(OwnerForm.ClearFilterItem);
                SetPriorityMenu();
                SetDateFilterMenu();
                OwnerForm.FilterColumnManager.InitGridView(gridView1);
            } else {
                lockUpdateCurrentMessage = false;
                FocusRow(focusedRowHandle);
            }
            gridControl1.Focus();
        }
        internal override void HideModule() {
            lockUpdateCurrentMessage = true;
            focusedRowHandle = gridView1.FocusedRowHandle;
        }
        protected override void LookAndFeelStyleChanged() {
            base.LookAndFeelStyleChanged();
            ColorHelper.UpdateColor(ilColumns, gridControl1.LookAndFeel);
        }
        private void gridView1_CustomDrawGroupRow(object sender, DevExpress.XtraGrid.Views.Base.RowObjectCustomDrawEventArgs e) {
            GridGroupRowInfo info = e.Info as GridGroupRowInfo;
            if(info == null) return;
            //info.GroupText = info.GroupText.Replace("1 items", "1 item");
        }

        private void gridView1_RowCellClick(object sender, DevExpress.XtraGrid.Views.Grid.RowCellClickEventArgs e) {
            if(e.Column == gcRead && e.Button == MouseButtons.Left)
                RaiseReadMessagesChanged(e.RowHandle);
            if(e.Column.FieldName == "Priority" && e.Button == MouseButtons.Left)
                PriorityMenu.ShowPopup(gridControl1.PointToScreen(e.Location));
            if(e.Button == MouseButtons.Right) ShowMessageMenu(gridControl1.PointToScreen(e.Location));
            if(e.Button == MouseButtons.Left && e.Clicks == 2) 
                EditMessage(e.RowHandle);
        }
        void EditMessage(int rowHandle) {
            if(rowHandle < 0) return;
            Message message = gridView1.GetRow(rowHandle) as Message;
            if(message != null)
                EditMessage(message, false, gcFrom.Caption);
        }
        private void gridView1_KeyDown(object sender, KeyEventArgs e) {
            if(e.KeyData == Keys.Enter)
                EditMessage(gridView1.FocusedRowHandle);
        }
        void RaiseReadMessagesChanged(int rowHandle) {
            Message current = gridView1.GetRow(rowHandle) as Message;
            if(current == null) return;
            current.ToggleRead();
            gridView1.LayoutChanged();
            OwnerForm.ReadMessagesChanged();
            MakeFocusedRowVisible();
        }
        void RaiseUpdateTreeViewMessages() {
            OwnerForm.UpdateTreeViewMessages();
        }
        void RaiseEnableDelete(bool enabled) {
            OwnerForm.EnableDelete(enabled);
        }
        private void RaiseEnableMail(bool enabled) {
            OwnerForm.EnableMail(enabled, enabled && CurrentMessage != null ? CurrentMessage.IsUnread : false);
        }
        void SetPriorityMenu() {
            OwnerForm.SetPriorityMenu(PriorityMenu);
        }
        void SetDateFilterMenu() {
            OwnerForm.SetDateFilterMenu(DateFilterMenu);
        }
        void ShowMessageMenu(Point location) {
            OwnerForm.ShowMessageMenu(location);
        }
        Message CurrentMessage {
            get { return currentMessage; }
            set {
                if(currentMessage == value) return;
                currentMessage = value;
                ucMailViewer1.ShowMessage(CurrentMessage);
                messageReadTimer.Stop();
                if(CurrentMessage != null && CurrentMessage.IsUnread)
                    messageReadTimer.Start();
            }
        }
        private void gridView1_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) {
            UpdateCurrentMessage();
        }
        private void gridView1_ColumnFilterChanged(object sender, EventArgs e) {
            UpdateCurrentMessage();
        }
        private void gridView1_SelectionChanged(object sender, DevExpress.Data.SelectionChangedEventArgs e) {
            RaiseEnableDelete(EnableDelete);
        }

        void UpdateCurrentMessage() {
            if(lockUpdateCurrentMessage) return;
            if(gridView1.FocusedRowHandle >= 0)
                CurrentMessage = gridView1.GetFocusedRow() as Message;
            else {
                List<Message> rows = new List<Message>();
                GridHelper.GetChildDataRowHandles(gridView1, gridView1.FocusedRowHandle, rows);
                ucMailViewer1.ShowMessagesInfo(rows);
                CurrentMessage = null;
                messageReadTimer.Stop();
            }
            RaiseEnableMail(gridView1.FocusedRowHandle >= 0);
            RaiseEnableDelete(EnableDelete);
        }
        
        protected internal override void ButtonClick(string tag) {
            switch(tag) {
                case TagResources.RotateLayout:
                    layoutControl1.Root.RotateLayout();
                    break;
                case TagResources.FlipLayout:
                    layoutControl1.Root.FlipLayout();
                    break;
                case TagResources.DeleteItem:
                    foreach(int row in gridView1.GetSelectedRows())
                        if(row >= 0) {
                            Message message = ((Message)gridView1.GetRow(row));
                            if(message.MailType == MailType.Deleted)
                                message.Deleted = true;
                            else
                                message.MailType = MailType.Deleted;
                        }
                    RaiseUpdateTreeViewMessages();
                    break;
                case TagResources.NewMail:
                    CreateNewMailMessage();
                    break;
                case TagResources.Reply:
                    CreateReplyMailMessage();
                    break;
                case TagResources.ReplyAll:
                    CreateReplyAllMailMessages();
                    break;
                case TagResources.Forward:
                    CreateForwardMailMessage();
                    break;
                case TagResources.UnreadRead:
                    foreach(int row in gridView1.GetSelectedRows())
                        if(row >= 0)
                            ((Message)gridView1.GetRow(row)).ToggleRead();
                    gridView1.LayoutChanged();
                    OwnerForm.ReadMessagesChanged();
                    break;
                case TagResources.CloseSearch:
                    gridView1.Focus();
                    break;
                case TagResources.ResetColumnsToDefault:
                    OwnerForm.FilterColumnManager.SetDefault();
                    break;
                case TagResources.ClearFilter:
                    gridView1.ActiveFilter.Clear();
                    break;
            }
        }
        bool EnableDelete {
            get {
                foreach (int row in gridView1.GetSelectedRows())
                    if (row >= 0)
                        return true;
                return false;
            }
        }

        void CreateNewMailMessage() {
            Message message = new Message();
            message.MailType = MailType.Draft;
            EditMessage(message, true, null);
        }
        void EditMessage(Message message, bool newMessage, string caption) {
            Cursor.Current = Cursors.WaitCursor;
            frmEditMail form = new frmEditMail(message, newMessage, caption);
            form.Load += OnEditMailFormLoad;
            form.FormClosed += OnEditMailFormClosed;
            form.Location = new Point(OwnerForm.Left + (OwnerForm.Width - form.Width) / 2, OwnerForm.Top + (OwnerForm.Height - form.Height) / 2);
            form.Show();
            Cursor.Current = Cursors.Default;
        }
        void CreateReplyAllMailMessages() {
            foreach (int row in gridView1.GetSelectedRows())
                CreateReplyMailMessage(row);
        }

        void CreateReplyMailMessage() {
            int[] rows = gridView1.GetSelectedRows();
            if (rows.Length != 1)
                return;
            CreateReplyMailMessage(rows[0]);
        }
        void CreateReplyMailMessage(int row) {
            if (row >= 0) {
                Message message = ((Message)gridView1.GetRow(row));
                if (message.MailType != MailType.Deleted && !message.Deleted)
                    CreateReplyMailMessage(message);
            }
        }
        void CreateReplyMailMessage(Message originalMessage) {
            Message message = new Message();
            message.MailType = MailType.Draft;
            message.From = originalMessage.From;
            message.Subject = originalMessage.Subject;
            message.Text = CreateReplyMessageText(originalMessage.Text, message.From, originalMessage.Date);
            message.IsReply = true;
            EditMessage(message, true, null);
        }
        void CreateForwardMailMessage() {
            int[] rows = gridView1.GetSelectedRows();
            if (rows.Length != 1)
                return;
            CreateForwardMailMessage(rows[0]);
        }
        void CreateForwardMailMessage(int row) {
            if (row >= 0) {
                Message message = ((Message)gridView1.GetRow(row));
                if (message.MailType != MailType.Deleted && !message.Deleted)
                    CreateForwardMailMessage(message);
            }
        }
        void CreateForwardMailMessage(Message originalMessage) {
            Message message = new Message();
            message.MailType = MailType.Draft;
            message.Subject = originalMessage.Subject;
            message.Text = CreateForwardMessageText(originalMessage.Text, String.Empty);
            EditMessage(message, true, null);
        }

        string CreateReplyMessageText(string text, string to, DateTime originalMessageDate) {
            using (RichEditDocumentServer server = new RichEditDocumentServer()) {
                server.HtmlText = text;
                QuoteReplyMessage(server, to, originalMessageDate);
                return server.HtmlText;
            }
        }
        string CreateForwardMessageText(string text, string to) {
            using (RichEditDocumentServer server = new RichEditDocumentServer()) {
                server.HtmlText = text;
                QuoteForwardMessage(server, to);
                return server.HtmlText;
            }
        }
        void QuoteReplyMessage(RichEditDocumentServer server, string to, DateTime originalMessageDate) {
            QuoteMessage(server);
            Document document = server.Document;
            string replyHeader = String.Format(
                Properties.Resources.ReplyText,
                to, originalMessageDate
            );
            document.InsertText(document.Range.Start, replyHeader);
        }
        void QuoteMessage(RichEditDocumentServer server) {
            Document document = server.Document;
            ParagraphCollection paragraphs = document.Paragraphs;
            foreach (Paragraph paragraph in paragraphs) {
                DocumentRange range = paragraph.Range;
                if (document.GetTableCell(range.Start) == null && !paragraph.IsInList) {
                    document.InsertText(range.Start, ">> ");
                }
            }
        }
        void QuoteForwardMessage(RichEditDocumentServer server, string to) {
            Document document = server.Document;
            string replyHeader = Properties.Resources.ForwardTextStart;
            document.InsertText(document.Range.Start, replyHeader);
            document.AppendText(Properties.Resources.ForwardTextStart);
        }
        void OnEditMailFormLoad(object sender, EventArgs e) {
            frmEditMail form = sender as frmEditMail;
            if (form != null)
                form.SaveMessage += OnEditMailFormSaveMessage;
        }

        void OnEditMailFormSaveMessage(object sender, EventArgs e) {
            frmEditMail form = sender as frmEditMail;
            if (form == null)
                return;

            if (!DataHelper.Messages.Contains(form.SourceMessage))
                DataHelper.Messages.Add(form.SourceMessage);
            RaiseUpdateTreeViewMessages();
        }

        void OnEditMailFormClosed(object sender, FormClosedEventArgs e) {
            frmEditMail form = sender as frmEditMail;
            if (form != null)
                form.SaveMessage -= OnEditMailFormSaveMessage;
        }
        protected internal override void MessagesDataChanged(DataSourceChangedEventArgs args) {
            partName = args.Caption;
            gridControl1.DataSource = args.List;
            if(args.Type == MailType.Deleted) {
                gcDate.Caption = Properties.Resources.DateDeleted;
                gcFrom.Caption = Properties.Resources.FromDeleted;
                OwnerForm.FilterColumnManager.UpdateColumnsCaption(Properties.Resources.DateDeleted, Properties.Resources.FromDeleted);
            } else if(args.Type == MailType.Inbox) {
                gcDate.Caption = Properties.Resources.DateInbox;
                gcFrom.Caption = Properties.Resources.FromInbox;
                OwnerForm.FilterColumnManager.UpdateColumnsCaption(Properties.Resources.DateInbox, Properties.Resources.FromInbox);
            } else {
                gcDate.Caption = Properties.Resources.DateOutbox;
                gcFrom.Caption = Properties.Resources.FromOutbox;
                OwnerForm.FilterColumnManager.UpdateColumnsCaption(Properties.Resources.DateOutbox, Properties.Resources.FromOutbox);
            }
            if(FindControl != null) {
                FindControl.FindEdit.Properties.NullValuePrompt = StringResources.GetSearchPrompt(args.Type);
                FindControl.FindEdit.Properties.NullValuePromptShowForEmptyValue = true;
                if(findControlManager == null)
                    findControlManager = new FindControlManager(ribbon, FindControl);
            }
            UpdateCurrentMessage();
        }
        FindControl FindControl {
            get {
                foreach(Control ctrl in gridControl1.Controls) {
                    FindControl ret = ctrl as FindControl;
                    if(ret != null) return ret;
                }
                return null;
            }
        }
        PopupMenu PriorityMenu {
            get {
                if(priorityMenu == null)
                    priorityMenu = new PriorityMenu(ribbon.Manager, gridView1, Properties.Resources.Low16x16, Properties.Resources.High16x16);
                return priorityMenu;
            }
        }
        PopupMenu DateFilterMenu {
            get {
                if(dateFilterMenu == null)
                    dateFilterMenu = new DateFilterMenu(ribbon.Manager, gridView1, filterCriteriaManager);
                return dateFilterMenu;
            }
        }
        void MakeFocusedRowVisible() {
            gridView1.MakeRowVisible(gridView1.FocusedRowHandle);
        }
        protected internal override void SendKeyDown(KeyEventArgs e) {
            base.SendKeyDown(e);
            if(e.KeyData == (Keys.E | Keys.Control)) {
                if(FindControl != null) {
                    FindControl.FindEdit.Focus();
                }
            }
        }
        private void gridView1_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e) {
            if(e.Column == gcSubject) {
                Message message = gridView1.GetRow(e.RowHandle) as Message;
                if(message != null)
                    e.DisplayText = message.SubjectDisplayText;
            }
        }
        protected override bool AllowZoomControl { get { return true; } }
        public override float ZoomFactor {
            get { return ucMailViewer1.ZoomFactor; }
            set { ucMailViewer1.ZoomFactor = value; }
        }
    }
}
