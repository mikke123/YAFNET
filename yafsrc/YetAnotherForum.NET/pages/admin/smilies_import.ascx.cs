using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using YAF.Classes.Utils;
using YAF.Classes.Data;

namespace YAF.Pages.Admin
{
	/// <summary>
	/// Summary description for smilies_import.
	/// </summary>
	public partial class smilies_import : YAF.Classes.Base.AdminPage
	{

		protected void Page_Load( object sender, System.EventArgs e )
		{
			if ( !IsPostBack )
			{
				PageLinks.AddLink( PageContext.BoardSettings.Name, YAF.Classes.Utils.YafBuildLink.GetLink( YAF.Classes.Utils.ForumPages.forum ) );
				PageLinks.AddLink( "Administration", YAF.Classes.Utils.YafBuildLink.GetLink( YAF.Classes.Utils.ForumPages.admin_admin ) );
				PageLinks.AddLink( "Smilies Import", "" );

				BindData();
			}
		}

		private void BindData()
		{
			using ( DataTable dt = new DataTable( "Files" ) )
			{
				dt.Columns.Add( "FileID", typeof( long ) );
				dt.Columns.Add( "FileName", typeof( string ) );
				DataRow dr = dt.NewRow();
				dr ["FileID"] = 0;
				dr ["FileName"] = "Select File (*.pak)";
				dt.Rows.Add( dr );

				System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo( Request.MapPath( String.Format( "{0}images/emoticons", YafForumInfo.ForumRoot ) ) );
				System.IO.FileInfo [] files = dir.GetFiles( "*.pak" );
				long nFileID = 1;
				foreach ( System.IO.FileInfo file in files )
				{
					dr = dt.NewRow();
					dr ["FileID"] = nFileID++;
					dr ["FileName"] = file.Name;
					dt.Rows.Add( dr );
				}

				File.DataSource = dt;
				File.DataValueField = "FileID";
				File.DataTextField = "FileName";
			}
			DataBind();
		}

		private void import_Click( object sender, System.EventArgs e )
		{
			if ( long.Parse( File.SelectedValue ) < 1 )
			{
				PageContext.AddLoadMessage( "You must select a .pak file to import." );
				return;
			}

			string fileName = Request.MapPath( String.Format( "{0}images/emoticons/{1}", YafForumInfo.ForumRoot, File.SelectedItem.Text ) );
			string split = System.Text.RegularExpressions.Regex.Escape( "=+:" );

			using ( System.IO.StreamReader file = new System.IO.StreamReader( fileName ) )
			{
				// Delete existing smilies?
				if ( DeleteExisting.Checked )
					YAF.Classes.Data.DB.smiley_delete( null );

				do
				{
					string line = file.ReadLine();
					if ( line == null )
						break;

					string [] lineSplit = System.Text.RegularExpressions.Regex.Split( line, split, System.Text.RegularExpressions.RegexOptions.None );

					if ( lineSplit.Length == 3 )
						YAF.Classes.Data.DB.smiley_save( null, PageContext.PageBoardID, lineSplit [2], lineSplit [0], lineSplit [1], 0, 0 );

				} while ( true );
				file.Close();

				// invalidate the cache...
				YafCache.Current.Remove( GetBoardCacheKey( Constants.Cache.Smilies ) );

			}
			YAF.Classes.Utils.YafBuildLink.Redirect( YAF.Classes.Utils.ForumPages.admin_smilies );
		}

		private void cancel_Click( object sender, System.EventArgs e )
		{
			YAF.Classes.Utils.YafBuildLink.Redirect( YAF.Classes.Utils.ForumPages.admin_smilies );
		}


		#region Web Form Designer generated code
		override protected void OnInit( EventArgs e )
		{
			import.Click += new System.EventHandler( import_Click );
			cancel.Click += new System.EventHandler( cancel_Click );
			//
			// CODEGEN: This call is required by the ASP.NET Web Form Designer.
			//
			InitializeComponent();
			base.OnInit( e );
		}

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
		}
		#endregion
	}
}
