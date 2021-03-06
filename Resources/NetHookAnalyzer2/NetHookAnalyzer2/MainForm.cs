﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Forms;
using WinForms = System.Windows.Forms;

namespace NetHookAnalyzer2
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
			Dump = new NetHookDump();

			selectedListViewItem = null;
			RepopulateInterface();

			itemsListView.ListViewItemSorter = new NetHookListViewItemSequentialComparer();
		}

		IDisposable itemsListViewFirstColumnHiderDisposable;

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			if (itemsListViewFirstColumnHiderDisposable != null)
			{
				itemsListViewFirstColumnHiderDisposable.Dispose();
				itemsListViewFirstColumnHiderDisposable = null;
			}

			base.OnFormClosed(e);
		}

		#region

		string GetLatestNethookDumpDirectory()
		{
			var steamDirectory = SteamUtils.GetSteamDirectory();
			if (steamDirectory == null)
			{
				return null;
			}

			var nethookDirectory = Path.Combine(steamDirectory, "nethook");

			if (!Directory.Exists(nethookDirectory))
			{
				return steamDirectory;
			}

			var nethookDumpDirs = Directory.GetDirectories(nethookDirectory);
			var latestDump = nethookDumpDirs.LastOrDefault();
			if (latestDump == null)
			{
				return nethookDirectory;
			}

			return Path.Combine(nethookDirectory, latestDump);
		}

		NetHookDump Dump { get; set; }

		void RepopulateInterface()
		{
			RepopulateListBox();
			RepopulateTreeView();
		}

		void RepopulateListBox()
		{
			var searchTerm = searchTextBox.Text;
			Expression<Func<NetHookItem, bool>> predicate;
			if (searchTerm == SearchTextBoxPlaceholderText || string.IsNullOrWhiteSpace(searchTerm))
			{
				predicate = nhi => true;
			}
			else
			{
				predicate = nhi => (nhi.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0) ||
					(nhi.InnerMessageName != null && nhi.InnerMessageName.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0);
			}

			var outAllowed = inOutRadioButton.Checked || outRadioButton.Checked;
			var inAllowed = inOutRadioButton.Checked || inRadioButton.Checked;
			Expression<Func<NetHookItem, bool>> directionPredicate = nhi => (nhi.Direction == NetHookItem.PacketDirection.Out && outAllowed) || (nhi.Direction == NetHookItem.PacketDirection.In && inAllowed);

			var listViewItems = Dump.Items.Where(directionPredicate).Where(predicate).Select(x => x.AsListViewItem());

			itemsListView.Items.Clear();
			itemsListView.Items.AddRange(listViewItems.ToArray());
		}

		#endregion

		#region UI Events

		void OnFormLoad(object sender, EventArgs e)
		{
			itemsListViewFirstColumnHiderDisposable = new ListViewColumnHider(itemsListView, 0);
		}

		void OnExitToolStripMenuItemClick(object sender, EventArgs e)
		{
			Application.Exit();
		}

		void OnOpenToolStripMenuItemClick(object sender, EventArgs e)
		{
			var dialog = new FolderBrowserDialog { ShowNewFolderButton = false };
			var latestNethookDir = GetLatestNethookDumpDirectory();
			if (latestNethookDir != null)
			{
				dialog.SelectedPath = GetLatestNethookDumpDirectory();
			}

			if (dialog.ShowDialog() != WinForms.DialogResult.OK)
			{
				return;
			}

			var dumpDirectory = dialog.SelectedPath;

			var dump = new NetHookDump();
			dump.LoadFromDirectory(dumpDirectory);
			Dump = dump;

			Text = string.Format("NetHook2 Dump Analyzer - [{0}]", dumpDirectory);

			selectedListViewItem = null;
			RepopulateInterface();

			if (itemsListView.Items.Count > 0)
			{
				itemsListView.Select();
				itemsListView.Items[0].Selected = true;
			}
		}

		void OnDirectionFilterCheckedChanged(object sender, EventArgs e)
		{
			RepopulateListBox();
		}

		void searchTextBox_TextChanged(object sender, EventArgs e)
		{
			RepopulateListBox();
		}

		#region SearchTextBox Placeholder Text

		const string SearchTextBoxPlaceholderText = "Search...";
		Color SearchTextBoxPlaceholderColor = Color.LightGray;
		Color SearchTextBoxUserTextColor = Color.Black;

		void OnSearchTextBoxEnter(object sender, EventArgs e)
		{
			if (searchTextBox.Text == SearchTextBoxPlaceholderText)
			{
				searchTextBox.Text = string.Empty;
				searchTextBox.ForeColor = SearchTextBoxUserTextColor;
			}
			else
			{
				searchTextBox.SelectAll();
			}
		}

		void OnSearchTextBoxLeave(object sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace(searchTextBox.Text))
			{
				searchTextBox.Text = SearchTextBoxPlaceholderText;
				searchTextBox.ForeColor = SearchTextBoxPlaceholderColor;
			}
		}

		#endregion


		#endregion

		ListViewItem selectedListViewItem;

		void OnItemsListViewSelectedIndexChanged(object sender, EventArgs e)
		{
			if (itemsListView.SelectedItems.Count != 1)
			{
				return;
			}

			var selectedItem = itemsListView.SelectedItems[0];
			if (selectedItem != selectedListViewItem)
			{
				selectedListViewItem = selectedItem;
				RepopulateTreeView();
			}
		}

		void RepopulateTreeView()
		{
			if (selectedListViewItem == null)
			{
				return;
			}

			var item = selectedListViewItem.GetNetHookItem();
			if (item == null)
			{
				return;
			}

			itemExplorerTreeView.Nodes.Clear();
			itemExplorerTreeView.Nodes.AddRange(item.BuildTree().Nodes.Cast<TreeNode>().ToArray());
			RecursiveExpandNodes(itemExplorerTreeView.Nodes.Cast<TreeNode>());
			itemExplorerTreeView.Nodes[0].EnsureVisible(); // Scroll to top
		}

		static void RecursiveExpandNodes(IEnumerable<TreeNode> nodes)
		{
			foreach (var node in nodes)
			{
				var shouldExpand = true;
				if (node.Tag != null)
				{
					var expansionInfo = (NetHookItemTreeBuilder.NodeInfo)node.Tag;
					shouldExpand = expansionInfo.ShouldExpandByDefault;
				}

				if (shouldExpand)
				{
					RecursiveExpandNodes(node.Nodes.Cast<TreeNode>());
					node.Expand();
				}
			}
		}
	}
}
