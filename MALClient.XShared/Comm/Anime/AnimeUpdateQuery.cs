﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Adapters;
using MALClient.Models.Enums;
using MALClient.Models.Models.Library;
using MALClient.XShared.Utils;
using MALClient.XShared.Utils.Managers;
using MALClient.XShared.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeUpdateQuery : Query
    {
        private readonly IAnimeData _item;
        public static bool SuppressOfflineSync { get; set; }
        public static bool UpdatedSomething { get; set; } //used for data saving on suspending in app.xaml.cs
        private static SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Just send rewatched value witch cannot be retrieved back
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rewatched"></param>
        public AnimeUpdateQuery(IAnimeData item, int rewatched)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<entry>");
            xml.AppendLine($"<times_rewatched>{rewatched}</times_rewatched>");
            xml.AppendLine("</entry>");


            Request =
                WebRequest.Create(Uri.EscapeUriString($"https://myanimelist.net/api/animelist/update/{item.Id}.xml?data={xml}"));
            Request.Credentials = Credentials.GetHttpCreditentials();
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
        }



        public AnimeUpdateQuery(IAnimeData item)
            : this(item.Id, item.MyEpisodes, (int)item.MyStatus, item.MyScore, item.StartDate, item.EndDate, item.Notes,item.IsRewatching)
        {
            _item = item;
            try
            {
                ResourceLocator.LiveTilesManager.UpdateTile(item);
            }
            catch (Exception)
            {
                //not windows
            }
        }


        private AnimeUpdateQuery(int id, int watchedEps, int myStatus, float myScore, string startDate, string endDate, string notes,bool rewatching)
        {
            UpdatedSomething = true;
            switch (CurrentApiType)
            {
                case ApiType.Mal:
                    UpdateAnimeMal(id, watchedEps, myStatus, (int)myScore, startDate, endDate, notes,rewatching);
                    break;
                case ApiType.Hummingbird:
                    UpdateAnimeHummingbird(id, watchedEps, myStatus, myScore, startDate, endDate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override async Task<string> GetRequestResponse()
        {

            try
            {
                await _updateSemaphore.WaitAsync();
                var result = "";
                try
                {
                    var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                    var updateHtml = await
                        client.GetStreamAsync($"https://myanimelist.net/ownlist/anime/{_item.Id}/edit?hideLayout=");
                    var doc = new HtmlDocument();
                    doc.Load(updateHtml);

                    var priority = doc.DocumentNode
                                       .FirstOfDescendantsWithId("select", "add_anime_priority")
                                       .Descendants("option")
                                       .First(node => node.Attributes.Contains("selected"))?
                                       .Attributes["value"].Value ?? "0";

                    var storage = doc.DocumentNode
                        .FirstOfDescendantsWithId("select", "add_anime_storage_type")
                        .Descendants("option")
                        .FirstOrDefault(node => node.Attributes.Contains("selected"))?
                        .Attributes["value"].Value;

                    var storageValue = doc.DocumentNode
                        .FirstOfDescendantsWithId("input", "add_anime_storage_value")
                        .Attributes["value"].Value;

                    var rewatches = doc.DocumentNode
                        .FirstOfDescendantsWithId("input", "add_anime_num_watched_times")
                        .Attributes["value"].Value;

                    var rewatchValue = doc.DocumentNode
                        .FirstOfDescendantsWithId("select", "add_anime_rewatch_value")
                        .Descendants("option")
                        .FirstOrDefault(node => node.Attributes.Contains("selected"))?
                        .Attributes["value"].Value;

                    var comments = doc.DocumentNode
                        .FirstOfDescendantsWithId("textarea", "add_anime_comments").InnerText;

                    var askDiscuss = doc.DocumentNode
                                         .FirstOfDescendantsWithId("select", "add_anime_is_asked_to_discuss")
                                         .Descendants("option")
                                         .FirstOrDefault(node => node.Attributes.Contains("selected"))?
                                         .Attributes["value"].Value ?? "0";

                    var postSns = doc.DocumentNode
                                      .FirstOfDescendantsWithId("select", "add_anime_sns_post_type")
                                      .Descendants("option")
                                      .FirstOrDefault(node => node.Attributes.Contains("selected"))?
                                      .Attributes["value"].Value ?? "0";


                    var content = new Dictionary<string, string>
                    {
                        ["anime_id"] = _item.Id.ToString(),
                        ["add_anime[status]"] = ((int) _item.MyStatus).ToString(),
                        ["add_anime[score]"] = _item.MyScore == 0 ? null : _item.MyScore.ToString(),
                        ["add_anime[num_watched_episodes]"] = _item.MyEpisodes.ToString(),
                        ["add_anime[tags]"] = string.IsNullOrEmpty(_item.Notes) ? "" : _item.Notes,                    

                        ["csrf_token"] = client.Token,

                        ["add_anime[priority]"] = priority,
                        ["add_anime[storage_type]"] = storage,
                        ["add_anime[storage_value]"] = storageValue,
                        ["add_anime[num_watched_times]"] = rewatches,
                        ["add_anime[rewatch_value]"] = rewatchValue,
                        ["add_anime[comments]"] = comments,
                        ["add_anime[is_asked_to_discuss]"] = askDiscuss,
                        ["add_anime[sns_post_type]"] = postSns,

                    };

                    if (_item.IsRewatching)
                        content["add_anime[is_rewatching]"] = "1";

                    if (_item.StartDate != null)
                    {
                        content["add_anime[start_date][month]"] = _item.StartDate.Substring(5, 2).TrimStart('0');
                        content["add_anime[start_date][day]"] = _item.StartDate.Substring(8, 2).TrimStart('0');
                        content["add_anime[start_date][year]"] = _item.StartDate.Substring(0, 4).Replace("0000","");
                    }

                    if (_item.EndDate != null)
                    {
                        content["add_anime[finish_date][month]"] = _item.EndDate.Substring(5, 2).TrimStart('0');
                        content["add_anime[finish_date][day]"] = _item.EndDate.Substring(8, 2).TrimStart('0');
                        content["add_anime[finish_date][year]"] = _item.EndDate.Substring(0, 4).Replace("0000","");
                    }

                    var response = await client.PostAsync(
                        $"https://myanimelist.net/ownlist/anime/{_item.Id}/edit?hideLayout",
                        new FormUrlEncodedContent(content));
                    if (!(await response.Content.ReadAsStringAsync()).Contains("badresult"))
                        result = "Updated";
                }
                catch (Exception e)
                {
#if ANDROID
                ResourceLocator.SnackbarProvider.ShowText("Failed to send update to MAL.");
#endif
                }

                if (string.IsNullOrEmpty(result) && !SuppressOfflineSync && Settings.EnableOfflineSync)
                {
                    result = "Updated";
                    Settings.AnimeSyncRequired = true;
                }

                ResourceLocator.ApplicationDataService[RoamingDataTypes.LastLibraryUpdate] = DateTime.Now.ToBinary();
                return result;
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        public override string SnackbarMessageOnFail => "Your changes will be synced with MAL on next app launch when online.";

        private void UpdateAnimeMal(int id, int watchedEps, int myStatus, int myScore, string startDate, string endDate, string notes,bool rewatching)
        {
            if (startDate != null)
            {
                var splitDate = startDate.Split('-');
                startDate = $"{splitDate[1]}{splitDate[2]}{splitDate[0]}";
            }
            if (endDate != null)
            {
                var splitDate = endDate.Split('-');
                endDate = $"{splitDate[1]}{splitDate[2]}{splitDate[0]}";
            }//mmddyyyy
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<entry>");
            xml.AppendLine($"<episode>{watchedEps}</episode>");
            xml.AppendLine($"<status>{myStatus}</status>");
            xml.AppendLine($"<score>{myScore}</score>");
            //xml.AppendLine("<download_episodes></download_episodes>");
            //xml.AppendLine("<storage_type></storage_type>");
            //xml.AppendLine("<storage_value></storage_value>");
            //xml.AppendLine("<times_rewatched></times_rewatched>");
            //xml.AppendLine("<rewatch_value></rewatch_value>");
            //if (startDate != null) xml.AppendLine($"<date_start>{startDate}</date_start>");
            //if (endDate != null) xml.AppendLine($"<date_finish>{endDate}</date_finish>");
            //xml.AppendLine("<priority></priority>");
            //xml.AppendLine("<enable_discussion></enable_discussion>");
            //xml.AppendLine($"<enable_rewatching>{(rewatching ? "1" : "0")}</enable_rewatching>");
            //xml.AppendLine("<comments></comments>");
            //xml.AppendLine("<fansub_group></fansub_group>");
            //xml.AppendLine($"<tags>{notes}</tags>");
            xml.AppendLine("</entry>");


            Request =
                WebRequest.Create(Uri.EscapeUriString($"https://myanimelist.net/api/animelist/update/{id}.xml?data={xml}"));
            Request.Credentials = Credentials.GetHttpCreditentials();
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "GET";
        }

        private void UpdateAnimeHummingbird(int id, int watchedEps, int myStatus, float myScore, string startDate,
            string endDate)
        {
            Request =
                WebRequest.Create(
                    Uri.EscapeUriString(
                        $"http://hummingbird.me/api/v1/libraries/{id}?auth_token={Credentials.HummingbirdToken}&episodes_watched={watchedEps}&rating={myScore}&status={AnimeStatusToHum((AnimeStatus) myStatus)}"));
            Request.ContentType = "application/x-www-form-urlencoded";
            {
                Request.Method = "POST";
            }
        }

        private static string AnimeStatusToHum(AnimeStatus status)
        {
            switch (status)
            {
                case AnimeStatus.Watching:
                    return "currently-watching";
                case AnimeStatus.PlanToWatch:
                    return "plan-to-watch";
                case AnimeStatus.Completed:
                    return "completed";
                case AnimeStatus.OnHold:
                    return "on-hold";
                case AnimeStatus.Dropped:
                    return "dropped";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}