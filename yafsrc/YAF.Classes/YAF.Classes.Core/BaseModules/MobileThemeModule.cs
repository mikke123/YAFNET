﻿/* Yet Another Forum.net
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
namespace YAF.Modules
{
  #region Using

  using System;
  using System.Web;

  using YAF.Classes.Core;
  using YAF.Classes.Pattern;
  using YAF.Classes.Utils;

  #endregion

  /// <summary>
  /// The mobile theme module.
  /// </summary>
  [YafModule("Mobile Theme Module", "Tiny Gecko", 1)]
  public class MobileThemeModule : IBaseModule
  {
    #region Properties

    /// <summary>
    ///   Gets or sets ForumControlObj.
    /// </summary>
    public object ForumControlObj { get; set; }

    #endregion

    #region Implemented Interfaces

    #region IBaseModule

    /// <summary>
    /// The init.
    /// </summary>
    public void Init()
    {
      YafContext.Current.AfterInit += this.Current_AfterInit;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// The dispose.
    /// </summary>
    public void Dispose()
    {
    }

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// The current_ after init.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    private void Current_AfterInit([NotNull] object sender, [NotNull] EventArgs e)
    {
      YafContext.Current.Vars["IsMobile"] = false;

      if (HttpContext.Current == null)
      {
        return;
      }

      // see if this is a mobile device...
      if (!UserAgentHelper.IsMobileDevice(HttpContext.Current.Request.UserAgent) &&
           !HttpContext.Current.Request.Browser.IsMobileDevice)
      {
        // make sure to shut off mobile theme usage if the user agent is not mobile.
        if (YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme ?? false)
        {
          YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme = false;
        }

        return;
      }

      // return if the user has mobile themes shut off in their profile.
      var userData = new CombinedUserDataHelper(YafContext.Current.PageUserID);
      if (!userData.UseMobileTheme)
      {
        return;
      }

      this.UpdateUseMobileThemeFromQueryString();

      // use the mobile theme?
      var useMobileTheme = YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme ?? true;

      // get the current mobile theme...
      var mobileTheme = YafContext.Current.BoardSettings.MobileTheme;

      if (mobileTheme.IsSet())
      {
        // create a new theme object...
        var theme = new YafTheme(mobileTheme);

        // make sure it's valid...
        if (YafTheme.IsValidTheme(theme.ThemeFile))
        {
          YafContext.Current.Vars["IsMobile"] = true;

          // set new mobile theme...
          if (useMobileTheme)
          {
            YafContext.Current.InstanceFactory.GetInstance<ThemeHandler>().Theme = theme;
            YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme = true;
          }

          return;
        }
      }

      // make sure to shut off mobile theme usage if there was no valid mobile theme found...
      if (YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme ?? false)
      {
        YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme = false;
      }
    }

    /// <summary>
    /// Updates the use mobile theme session variable from the query string.
    /// </summary>
    private void UpdateUseMobileThemeFromQueryString()
    {
      var fullSite = HttpContext.Current.Request.QueryString.GetFirstOrDefault("fullsite");
      if (fullSite.IsSet() && fullSite.Equals("true"))
      {
        YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme = false;
      }

      var mobileSite = HttpContext.Current.Request.QueryString.GetFirstOrDefault("mobileSite");
      if (mobileSite.IsSet() && mobileSite.Equals("true"))
      {
        YafContext.Current.InstanceFactory.GetInstance<YafSession>().UseMobileTheme = true;
      }
    }

    #endregion
  }
}