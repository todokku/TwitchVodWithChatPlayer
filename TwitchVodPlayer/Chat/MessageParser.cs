﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace TwitchVodPlayer.Chat {
    class MessageParser {

        //Fields

        private readonly int parsingWordMaxLength = 20;
        private readonly int parsingWordMinLength = 2;

        //Initialization

        public MessageParser() {

        }

        //Methods

        public string ParseTwitchEmoticons(dynamic twitchEmoticons, string channelId, string messageBody) {
            Fetching.Emoticons.TwitchEmoticonDownloader twitchEmoticonDownloader = new Fetching.Emoticons.TwitchEmoticonDownloader();
            //TWITCH
            string convertedMessageBody = messageBody;
            int indexOffset = 0;
            foreach (var emoticon in twitchEmoticons) {
                string emoticonPath = Fetching.Constants.EmoticonsPath + Fetching.Constants.TwitchEmoticonsDirectoryName + @"/" + emoticon._id + Fetching.Constants.EmoticonFileExtension;

                if (!File.Exists(emoticonPath)) {
                    twitchEmoticonDownloader.DownloadEmoticon(emoticon._id.ToString(), emoticonPath);
                    continue;
                }

                StringBuilder stringBuilder = new StringBuilder(convertedMessageBody);
                try {
                    stringBuilder.Remove((int)emoticon.begin + indexOffset, (int)emoticon.end - (int)emoticon.begin + 1);
                } catch (Exception e) {
                    Console.WriteLine("Failed parsing Twitch emoticon: " + e.Message);
                    return messageBody;
                }

                string htmlImageTag = Chat.Constants.HtmlImageTagBegin + emoticonPath + Chat.Constants.HtmlImageTagEnd;
                try {
                    stringBuilder.Insert((int)emoticon.begin + indexOffset, htmlImageTag);
                }catch (Exception e) {
                    Console.WriteLine("Failed parsing Twitch emoticon: " + e.Message);
                    return messageBody;
                }

                indexOffset += (htmlImageTag.Length) - ((int)emoticon.end - (int)emoticon.begin + 1);
                convertedMessageBody = stringBuilder.ToString();
            }
            return convertedMessageBody;
        }

        public string ParseDictionaryEmoticons(Dictionary<string, string> emoticonDictionary, Fetching.Emoticons.EmoticonDownloader emoticonDownloader, string directoryName, string channelId, string messageBody) {
            if (emoticonDictionary == null) {
                return messageBody;
            }

            string convertedMessageBody = "";
            string convertedMessageBodyWord;

            string[] messageBodyWords = messageBody.Split(' ');

            foreach (string messageBodyWord in messageBodyWords) {
                convertedMessageBodyWord = messageBodyWord + " ";
                if (messageBodyWord == "" || messageBodyWord.Length > parsingWordMaxLength || messageBodyWord.Length < parsingWordMinLength) {
                    convertedMessageBody += convertedMessageBodyWord;
                    continue;
                }
                string emoticonKey = null;
                string emoticonValue = null;

                if (emoticonDictionary.ContainsKey(messageBodyWord + Fetching.Constants.EmoticonFileExtension)) {
                    emoticonKey = messageBodyWord + Fetching.Constants.EmoticonFileExtension;
                    emoticonValue = emoticonDictionary[messageBodyWord + Fetching.Constants.EmoticonFileExtension];
                } else if (emoticonDictionary.ContainsKey(messageBodyWord + Fetching.Constants.GifEmoticonFileExtension)) {
                    emoticonKey = messageBodyWord + Fetching.Constants.GifEmoticonFileExtension;
                    emoticonValue = emoticonDictionary[messageBodyWord + Fetching.Constants.GifEmoticonFileExtension];
                } else {
                    convertedMessageBody += convertedMessageBodyWord;
                    continue;
                }

                string emoticonPath;
                string emoticonFileName = Regex.Replace(HttpUtility.UrlEncode(emoticonKey), "[^.A-Za-z0-9]", "");
                if (channelId == "") {
                    emoticonPath = Fetching.Constants.EmoticonsPath + directoryName + @"/" + emoticonFileName;
                } else {
                    emoticonPath = Fetching.Constants.EmoticonsPath + directoryName + @"/" + channelId + @"/" + emoticonFileName;
                }

                if (!File.Exists(emoticonPath)) {
                    emoticonDownloader.DownloadEmoticon(emoticonValue, emoticonPath);
                }

                convertedMessageBodyWord = (Chat.Constants.HtmlImageTagBegin + emoticonPath + Chat.Constants.HtmlImageTagEnd) + " ";

                convertedMessageBody += convertedMessageBodyWord;
            }

            return convertedMessageBody;
        }

        public string ParseBadgeSet(dynamic deserializedBadgeSet, dynamic userBadges, string channelId, string badgeBody) {
            Fetching.Badges.BadgeDownloader badgeDownloader = new Fetching.Badges.BadgeDownloader();

            string convertedBadgeBody = badgeBody;
            if (deserializedBadgeSet == null) {
                return convertedBadgeBody;
            }
            foreach (dynamic userBadge in userBadges) {
                if (deserializedBadgeSet["badge_sets"][userBadge._id.ToString()] == null || deserializedBadgeSet["badge_sets"][userBadge._id.ToString()]["versions"][userBadge.version.ToString()] == null) {
                    continue;
                }

                string badge = deserializedBadgeSet["badge_sets"][userBadge._id.ToString()]["versions"][userBadge.version.ToString()].ToString();

                string badgeUrl = badge.Split(new char[] { ':' }, 2)[1].Split(',')[0].Replace("\"", "");

                string badgePath;
                string badgeFileName = userBadge._id.ToString() + "_" + userBadge.version.ToString();
                if (channelId == "") {
                    badgePath = Fetching.Constants.BadgesPath + badgeFileName + Fetching.Constants.BadgeFileExtension;
                } else {
                    badgePath = Fetching.Constants.BadgesPath + channelId + @"/" + badgeFileName + Fetching.Constants.BadgeFileExtension;
                }

                if (!File.Exists(badgePath)) {
                    badgeDownloader.DownloadBadge(badgeUrl, badgePath);
                }

                string htmlImageTag = Chat.Constants.HtmlImageTagBegin + badgePath + Chat.Constants.HtmlImageTagEnd;
                convertedBadgeBody += htmlImageTag;

            }
            return convertedBadgeBody;

        }

        public dynamic GetBadgeSet(string channelId) {
            dynamic badgeSet = null;
            string jsonPath;
            if (channelId == "") {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.BadgeJsonFileName + ".json";
            } else {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.BadgeJsonFileName + "_" + channelId + ".json";
            }
            if (!File.Exists(jsonPath)) {
                return null;
            }
            using (WebClient client = new WebClient()) {
                using (Stream stream = client.OpenRead(jsonPath)) {
                    using (StreamReader streamReader = new StreamReader(stream)) {
                        using (JsonTextReader reader = new JsonTextReader(streamReader)) {
                            reader.SupportMultipleContent = true;

                            var serializer = new JsonSerializer();
                            while (reader.Read()) {
                                if (reader.TokenType != JsonToken.StartObject) {
                                    continue;
                                }
                                badgeSet = serializer.Deserialize<dynamic>(reader);
                            }
                        }
                    }
                }
            }
            return badgeSet;
        }

        public Dictionary<string, string> GetFfzEmoticonDictionary(string channelId) {
            Dictionary<string, string> ffzEmoticonDictionary = new Dictionary<string, string>();
            string jsonPath;
            if (channelId == "") {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.FfzEmoticonJsonFileName + ".json";
            } else {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.FfzEmoticonJsonFileName + "_" + channelId + ".json";
            }
            if (!File.Exists(jsonPath)) {
                return null;
            }
            using (WebClient client = new WebClient()) {
                using (Stream stream = client.OpenRead(jsonPath)) {
                    using (StreamReader streamReader = new StreamReader(stream)) {
                        using (JsonTextReader reader = new JsonTextReader(streamReader)) {
                            reader.SupportMultipleContent = true;

                            var serializer = new JsonSerializer();
                            bool insideArray = false;
                            while (reader.Read()) {
                                if (reader.TokenType == JsonToken.StartArray && !insideArray) {
                                    insideArray = true;
                                    continue;
                                }
                                if (!insideArray || reader.TokenType != JsonToken.StartObject) {
                                    continue;
                                }

                                var emoticon = serializer.Deserialize<dynamic>(reader);

                                string[] urls = ((string)emoticon.urls.ToString()).Split(',');
                                string[] separatedFirstURL = urls[0].Split(':');
                                int firstIndex = separatedFirstURL[1].IndexOf('"');
                                int lastIndex = separatedFirstURL[1].LastIndexOf('"');
                                string emoticonUrl = "https:" + separatedFirstURL[1].Substring(firstIndex + 1, lastIndex - firstIndex - 1);

                                try {
                                    ffzEmoticonDictionary.Add(emoticon.name.ToString() + Fetching.Constants.EmoticonFileExtension, emoticonUrl);
                                } catch { }
                            }
                        }
                    }
                }
            }
            return ffzEmoticonDictionary.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }

        class LengthComparer : IComparer<String> {
            public int Compare(string x, string y) {
                int lengthComparison = x.Length.CompareTo(y.Length);
                if (lengthComparison == 0) {
                    return x.CompareTo(y);
                } else {
                    return lengthComparison;
                }
            }
        }

        public Dictionary<string, string> GetBttvEmoticonDictionary(string channelId) {
            Dictionary<string, string> bttvEmoticonDictionary = new Dictionary<string, string>();
            string jsonPath;
            if (channelId == "") {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.BttvEmoticonJsonFileName + ".json";
            } else {
                jsonPath = Fetching.Constants.JsonPath + Fetching.Constants.BttvEmoticonJsonFileName + "_" + channelId + ".json";
            }
            if (!File.Exists(jsonPath)) {
                return null;
            }
            using (WebClient client = new WebClient()) {
                using (Stream stream = client.OpenRead(jsonPath)) {
                    using (StreamReader streamReader = new StreamReader(stream)) {
                        using (JsonTextReader reader = new JsonTextReader(streamReader)) {
                            reader.SupportMultipleContent = true;

                            var serializer = new JsonSerializer();
                            bool insideArray = false;
                            while (reader.Read()) {
                                if (reader.TokenType == JsonToken.StartArray && !insideArray) {
                                    insideArray = true;
                                    continue;
                                }
                                if (!insideArray || reader.TokenType != JsonToken.StartObject) {
                                    continue;
                                }

                                var emoticon = serializer.Deserialize<dynamic>(reader);

                                string emoticonUrl = @"https://cdn.betterttv.net/emote/" + emoticon.id + @"/" + Fetching.Constants.BttvEmoticonSize;
                                try {
                                    bttvEmoticonDictionary.Add(emoticon.code.ToString() + "." + emoticon.imageType.ToString(), emoticonUrl);
                                } catch { }
                            }
                        }
                    }
                }
            }
            return bttvEmoticonDictionary = bttvEmoticonDictionary.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
