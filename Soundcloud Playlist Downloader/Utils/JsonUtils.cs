﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soundcloud_Playlist_Downloader.JsonObjects;

namespace Soundcloud_Playlist_Downloader.Utils
{
    public static class JsonUtils
    {
        public static string RetrieveJson(string url, int? limit = null, int? offset = null)
        {
            string json = null;
            if (limit == 0)
                limit = null;

            if (string.IsNullOrEmpty(url))
                return null;
            try
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    if (!url.Contains("client_id="))
                    {
                        url += (url.Contains("?") ? "&" : "?") + "client_id=" + DownloadUtils.ClientId;
                    }
                    if (limit != null)
                    {
                        url += "&limit=" + limit;
                    }
                    if (offset != null)
                    {
                        url += "&offset=" + offset;
                    }
                    if (limit != null)
                        url += "&linked_partitioning=1"; //will add next_href to the response

                    json = client.DownloadString(url);
                }
            }
            catch (Exception e)
            {
                SoundcloudSync.IsError = true;
                ExceptionHandlerUtils.HandleException(e);
            }

            return json;
        }

        public static string RetrievePlaylistId(string userApiUrl, string playlistName)
        {
            // parse each playlist out, match the name based on the
            // permalink, and return the id of the matching playlist.           
            var playlistsJson = RetrieveJson(userApiUrl);

            var playlists = JArray.Parse(playlistsJson);
            IList<JToken> results = playlists.Children().ToList();
            IList<PlaylistItem> playlistsitems = new List<PlaylistItem>();

            foreach (var result in results)
            {
                var playlistsitem = JsonConvert.DeserializeObject<PlaylistItem>(result.ToString());
                playlistsitems.Add(playlistsitem);
            }

            var matchingPlaylistItem = playlistsitems.FirstOrDefault(s => s.permalink == playlistName);

            if (matchingPlaylistItem != null)
            {
                return matchingPlaylistItem.id.ToString();
            }
            SoundcloudSync.IsError = true;
            throw new Exception("Unable to find a matching playlist");
        }

        public static Track RetrieveTrackFromUrl(string url)
        {
            var trackJson = RetrieveJson("https://api.soundcloud.com/resolve.json?url=" + url);
            JObject track = JObject.Parse(trackJson);
            if (track?.GetValue("id") != null)
                return JsonConvert.DeserializeObject<Track>(track.ToString());

            return null;
        }

        public static IList<Track> RetrieveTracksFromUrl(string url, bool isRawTracksUrl, bool ignoreSampleSongs)
        {
            var limit = isRawTracksUrl ? 200 : 0; //200 is the limit set by SoundCloud itself. Remember; limits are only with 'collection' types in JSON 
            IList<Track> tracks = new List<Track>();
            var lastStep = false;
            try
            {
                var tracksJson = RetrieveJson(url, limit);
                while (tracksJson != null)
                {
                    var JOBtracksJson = JObject.Parse(tracksJson);
                    IList<JToken> JTOKENcurrentTracks = isRawTracksUrl
                        ? JOBtracksJson["collection"].Children().ToList()
                        : JOBtracksJson["tracks"].Children().ToList();

                    IList<Track> currentTracks = new List<Track>();
                    foreach (var Jtrack in JTOKENcurrentTracks)
                    {
                        var currentTrack = JsonConvert.DeserializeObject<Track>(Jtrack.ToString());
                        currentTracks.Add(currentTrack);
                    }

                    foreach (var track in currentTracks)
                    {
                        //If it's a preview song (SNIP), ignore the track and do not retrieve
                        if (ignoreSampleSongs)
                        {
                            if (track.policy != "SNIP")
                                tracks.Add(track);
                        }
                        else
                        {
                            tracks.Add(track);
                        }
                    }
                    if (lastStep)
                        break;

                    var linkedPartitioningUrl = JsonConvert.DeserializeObject<NextInfo>(tracksJson).next_href;
                    tracksJson = RetrieveJson(linkedPartitioningUrl);
                    if (!string.IsNullOrEmpty(tracksJson)) continue;
                    lastStep = true;
                }
            }
            catch (Exception)
            {
                SoundcloudSync.IsError = true;
                throw new Exception("Errors occurred retrieving the tracks list information. Double check your url.");
            }
            return tracks;
        }
    }
}
