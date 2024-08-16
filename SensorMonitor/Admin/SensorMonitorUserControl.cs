using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.UI;

namespace SensorMonitor.Admin
{
	public partial class SensorMonitorUserControl : UserControl
	{
		internal event EventHandler ConfigurationChangedByUser;
		internal Item _selectedCameraItem;
        internal Item _selectedEventItem;
	    internal Item _item;

		public SensorMonitorUserControl(string labelName)
		{
			InitializeComponent();
			label1.Text = labelName;
		}

		internal String DisplayName
		{
			get { return textBoxName.Text; }
			set { textBoxName.Text = value; }
		}

		internal Item RelatedCamera
		{
			set { 
				_selectedCameraItem = value;
				buttonCameraSelect.Text = value.Name;
			}
			get { return _selectedCameraItem; }
		}

	    internal Item RelatedEvent
	    {
            set
            {
                _selectedEventItem = value;
                buttonEventSelect.Text = value.Name;
            }
            get { return _selectedEventItem; }
        }

	    internal bool Disabled
	    {
            get { return checkBoxDisabled.Checked; }
            set { checkBoxDisabled.Checked = value; }
	    }

		internal void OnUserChange(object sender, EventArgs e)
		{
			if (ConfigurationChangedByUser != null)
				ConfigurationChangedByUser(this, new EventArgs());
		}

		internal void FillContent(Item item)
		{
			textBoxName.Text = item.Name;
		    _item = item;
			if (item.Properties.ContainsKey("RelatedFQID"))
			{
				FQID fqid = new FQID(item.Properties["RelatedFQID"]);
				Item relatedItem = Configuration.Instance.GetItem(fqid);
				if (relatedItem != null)
					RelatedCamera = relatedItem;
			}
            if (item.Properties.ContainsKey("RelatedEventFQID"))
            {
                FQID fqid = new FQID(item.Properties["RelatedEventFQID"]);
                Item relatedItem = Configuration.Instance.GetItem(fqid);
                if (relatedItem != null)
                    RelatedEvent = relatedItem;
            }
		    if (item.Properties.ContainsKey("Enabled"))
		    {
		        Disabled = item.Properties["Enabled"] == "No";
		    }
		}

		internal void UpdateItem(Item item)
		{
			item.Name = DisplayName;

			if (_selectedCameraItem != null)
				item.Properties["RelatedFQID"] = _selectedCameraItem.FQID.ToXmlNode().OuterXml;
            if (_selectedEventItem != null)
                item.Properties["RelatedEventFQID"] = _selectedEventItem.FQID.ToXmlNode().OuterXml;
            item.Properties["Enabled"] = checkBoxDisabled.Checked? "No":"Yes";
        }

		internal void ClearContent()
		{
			textBoxName.Text = "";
			buttonCameraSelect.Text = "Select camera...";
            buttonEventSelect.Text = "Select event...";
		}

		private void OnCameraSelect(object sender, EventArgs e)
		{
            var form = new ItemPickerWpfWindow()
            {
                Items = Configuration.Instance.GetItems(),
                KindsFilter = new List<Guid>() { Kind.Camera },
				SelectionMode = SelectionModeOptions.AutoCloseOnSelect,
                SelectedItems = new List<Item> { _selectedCameraItem }
            };

			if (form.ShowDialog().Value)
			{
				_selectedCameraItem = form.SelectedItems.First();
				buttonCameraSelect.Text = "";
				if (_selectedCameraItem != null)
				{
					buttonCameraSelect.Text = _selectedCameraItem.Name;
					OnUserChange(this, null);
				}
			}
		}

        private void OnEventSelect(object sender, EventArgs e)
        {
            var form = new ItemPickerWpfWindow()
            {
                Items = Configuration.Instance.GetItems(),
                KindsFilter = new List<Guid>() { Kind.TriggerEvent },
                SelectionMode = SelectionModeOptions.AutoCloseOnSelect,
                SelectedItems = new List<Item> { _selectedEventItem }
            };

            if (form.ShowDialog().Value)
			{
				_selectedEventItem = form.SelectedItems.First();
                buttonEventSelect.Text = "";
                if (_selectedEventItem != null)
				{
                    buttonEventSelect.Text = _selectedEventItem.Name;
					OnUserChange(this, null);
				}
			}
        }

	}
}
