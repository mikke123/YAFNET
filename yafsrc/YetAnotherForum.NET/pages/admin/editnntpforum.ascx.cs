/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bj�rnar Henden
 * Copyright (C) 2006-2010 Jaben Cargman
 * http://www.yetanotherforum.net/
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 */

namespace YAF.Pages.Admin
{
  #region Using

  using System;
  using System.Data;
  using System.Web;

  using YAF.Classes.Data;
  using YAF.Core;
  using YAF.Types;
  using YAF.Types.Constants;
  using YAF.Types.Interfaces;
  using YAF.Utils;

  #endregion

  /// <summary>
  /// Summary description for editgroup.
  /// </summary>
  public partial class editnntpforum : AdminPage
  {
    #region Methods

    /// <summary>
    /// The cancel_ click.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    protected void Cancel_Click([NotNull] object sender, [NotNull] EventArgs e)
    {
      YafBuildLink.Redirect(ForumPages.admin_nntpforums);
    }

    /// <summary>
    /// The page_ load.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    protected void Page_Load([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (!this.IsPostBack)
      {
        this.PageLinks.AddLink(this.PageContext.BoardSettings.Name, YafBuildLink.GetLink(ForumPages.forum));
        this.PageLinks.AddLink(this.GetText("ADMIN_ADMIN", "Administration"), string.Empty);
        this.PageLinks.AddLink("NNTP Forums", string.Empty);

        this.BindData();
        if (this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("s") != null)
        {
          using (
            DataTable dt = LegacyDb.nntpforum_list(
              this.PageContext.PageBoardID, 
              null, 
              this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("s"), 
              DBNull.Value))
          {
            DataRow row = dt.Rows[0];
            this.NntpServerID.Items.FindByValue(row["NntpServerID"].ToString()).Selected = true;
            this.GroupName.Text = row["GroupName"].ToString();
            this.ForumID.Items.FindByValue(row["ForumID"].ToString()).Selected = true;
            this.Active.Checked = (bool)row["Active"];
          }
        }
      }
    }

    /// <summary>
    /// The save_ click.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    protected void Save_Click([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (this.GroupName.Text.Trim().IsNotSet())
      {
        this.PageContext.LoadMessage.Add("You should enter a valid group name.");
        return;
      }

      object nntpForumID = null;
      if (this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("s") != null)
      {
        nntpForumID = this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("s");
      }

      if (this.ForumID.SelectedValue.ToType<int>() <= 0)
      {
        this.PageContext.AddLoadMessage("You must select a forum to save NNTP messages.");
        return;
      }

      LegacyDb.nntpforum_save(
        nntpForumID, 
        this.NntpServerID.SelectedValue, 
        this.GroupName.Text, 
        this.ForumID.SelectedValue, 
        this.Active.Checked);
      YafBuildLink.Redirect(ForumPages.admin_nntpforums);
    }

    /// <summary>
    /// The bind data.
    /// </summary>
    private void BindData()
    {
      this.NntpServerID.DataSource = LegacyDb.nntpserver_list(this.PageContext.PageBoardID, null);
      this.NntpServerID.DataValueField = "NntpServerID";
      this.NntpServerID.DataTextField = "Name";
      this.ForumID.DataSource = LegacyDb.forum_listall_sorted(this.PageContext.PageBoardID, this.PageContext.PageUserID);
      this.ForumID.DataValueField = "ForumID";
      this.ForumID.DataTextField = "Title";
      this.DataBind();
    }

    #endregion
  }
}