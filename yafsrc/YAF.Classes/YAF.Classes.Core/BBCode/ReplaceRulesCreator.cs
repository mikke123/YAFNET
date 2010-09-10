/* Yet Another Forum.net
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
namespace YAF.Classes.Core.BBCode
{
  #region Using

  using System;

  using YAF.Classes.Data;
  using YAF.Classes.Utils;

  #endregion

  /// <summary>
  /// Gets an instance of replace rules and uses
  ///   caching if possible.
  /// </summary>
  public static class ReplaceRulesCreator
  {
    #region Public Methods

    /// <summary>
    /// Clears all ReplaceRules from the cache.
    /// </summary>
    public static void ClearCache()
    {
      // match starting part of cache key
      string match = Constants.Cache.ReplaceRules.FormatWith(string.Empty);

      // remove it entries from cache
      YafContext.Current.Cache.Remove((x) => x.StartsWith(match));
    }

    /// <summary>
    /// Gets relace rules instance for given flags.
    /// </summary>
    /// <param name="uniqueFlags">
    /// Flags identifying replace rules.
    /// </param>
    /// <returns>
    /// ReplaceRules for given unique flags.
    /// </returns>
    public static ProcessReplaceRules GetInstance(bool[] uniqueFlags)
    {
      // convert flags to integer
      int rulesFlags = FlagsBase.GetIntFromBoolArray(uniqueFlags);

      // cache is board-specific since boards may have different custom BB Code...
      string key = YafCache.GetBoardCacheKey(Constants.Cache.ReplaceRules.FormatWith(rulesFlags));

      ProcessReplaceRules rules = YafContext.Current.Cache.GetItem(
        key, YafContext.Current.BoardSettings.ReplaceRulesCacheTimeout, () => new ProcessReplaceRules());
      return rules;
    }

    #endregion
  }
}