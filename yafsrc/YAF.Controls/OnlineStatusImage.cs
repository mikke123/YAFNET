﻿/* Yet Another Forum.NET
 * Copyright (C) 2006-2009 Jaben Cargman
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
using System.Data;
using System.Linq;
using YAF.Classes;
using YAF.Classes.Core;
using YAF.Classes.Data;
using YAF.Classes.Utils;

namespace YAF.Controls
{
	/// <summary>
	/// Provides an Online/Offline status for a YAF User
	/// </summary>
	public class OnlineStatusImage : ThemeImage
	{
		public OnlineStatusImage()
		{
		}

		protected override void Render( System.Web.UI.HtmlTextWriter output )
		{
			LocalizedTitlePage = "POSTS";

			if ( Visible )
			{
				string cacheKey = YafCache.GetBoardCacheKey( Constants.Cache.UsersOnlineStatus );
				DataTable activeUsers = PageContext.Cache.GetItem<DataTable>( cacheKey, YafContext.Current.BoardSettings.OnlineStatusCacheTimeout, () =>
																																		 DB.active_list(
																																		 YafContext.Current.PageBoardID, false,
																																		 YafContext.Current.BoardSettings.ActiveListTime, false ) );

				if ( activeUsers.AsEnumerable().Any( x => x.Field<int>( "UserId" ) == UserID && !x.Field<bool>( "IsHidden" ) ) )
				{
					// online
					LocalizedTitleTag = "USERONLINESTATUS";
					ThemeTag = "USER_ONLINE";
					Alt = "Online";
				}
				else
				{
					// offline
					LocalizedTitleTag = "USEROFFLINESTATUS";
					ThemeTag = "USER_OFFLINE";
					Alt = "Offline";
				}
			}
			
			base.Render( output );
		}

		/// <summary>
		/// The userid of the user.
		/// </summary>
		public int UserID
		{
			get
			{
				return ViewState["UserID"].ToType<int>();
			}
			set
			{
				ViewState["UserID"] = value;
			}
		}
	}
}
