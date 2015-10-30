﻿using GitterSharp.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using GitterSharp.Model;
using System.Reactive;
using System.Net.Http;
using System.Net.Http.Headers;
using GitterSharp.Configuration;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Newtonsoft.Json;
using GitterSharp.Helpers;
using System.Text;

namespace GitterSharp.UniversalWindows.Services
{
    public class ReactiveGitterApiService : IReactiveGitterApiService
    {
        #region Fields

        private readonly string _baseApiAddress = $"{Constants.ApiBaseUrl}{Constants.ApiVersion}";
        private readonly string _baseStreamingApiAddress = $"{Constants.StreamApiBaseUrl}{Constants.ApiVersion}";

        private HttpClient HttpClient
        {
            get
            {
                var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(Token))
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

                return httpClient;
            }
        }

        #endregion

        #region Properties

        public string Token { get; set; }

        #endregion

        #region Constructors

        public ReactiveGitterApiService() { }

        public ReactiveGitterApiService(string token)
        {
            Token = token;
        }

        #endregion

        #region User

        public IObservable<User> GetCurrentUser()
        {
            string url = _baseApiAddress + "user";
            return HttpClient.GetAsync<IEnumerable<User>>(url)
                .ToObservable()
                .Select(users => users.FirstOrDefault());
        }

        public IObservable<IEnumerable<Organization>> GetOrganizations(string userId)
        {
            string url = _baseApiAddress + $"user/{userId}/orgs";
            return HttpClient.GetAsync<IEnumerable<Organization>>(url)
                .ToObservable();
        }

        public IObservable<IEnumerable<Repository>> GetRepositories(string userId)
        {
            string url = _baseApiAddress + $"user/{userId}/repos";
            return HttpClient.GetAsync<IEnumerable<Repository>>(url)
                .ToObservable();
        }

        #endregion

        #region Unread Items

        public IObservable<UnreadItems> RetrieveUnreadChatMessages(string userId, string roomId)
        {
            string url = _baseApiAddress + $"user/{userId}/rooms/{roomId}/unreadItems";
            return HttpClient.GetAsync<UnreadItems>(url)
                .ToObservable();
        }

        public IObservable<Unit> MarkUnreadChatMessages(string userId, string roomId, IEnumerable<string> messageIds)
        {
            string url = _baseApiAddress + $"user/{userId}/rooms/{roomId}/unreadItems";
            var content = new StringContent("{\"chat\": " + JsonConvert.SerializeObject(messageIds) + "}",
                Encoding.UTF8,
                "application/json");

            return HttpClient.PostAsync(url, content)
                .ToObservable()
                .Select(x => new Unit());
        }

        #endregion

        #region Rooms

        public IObservable<IEnumerable<Room>> GetRooms()
        {
            string url = _baseApiAddress + "rooms";
            return HttpClient.GetAsync<IEnumerable<Room>>(url)
                .ToObservable();
        }

        public IObservable<Room> JoinRoom(string roomName)
        {
            string url = _baseApiAddress + "rooms";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"uri", roomName}
            });

            return HttpClient.PostAsync<Room>(url, content)
                .ToObservable();
        }

        #endregion

        #region Messages

        public IObservable<Message> GetSingleRoomMessage(string roomId, string messageId)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages/{messageId}";
            return HttpClient.GetAsync<Message>(url)
                .ToObservable();
        }

        public IObservable<IEnumerable<Message>> GetRoomMessages(string roomId, int limit = 50, string beforeId = null, string afterId = null, int skip = 0)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages?limit={limit}";

            if (!string.IsNullOrWhiteSpace(beforeId))
                url += $"&beforeId={beforeId}";

            if (!string.IsNullOrWhiteSpace(afterId))
                url += $"&afterId={afterId}";

            if (skip > 0)
                url += $"&skip={skip}";

            return HttpClient.GetAsync<IEnumerable<Message>>(url)
                .ToObservable();
        }

        public IObservable<Message> SendMessage(string roomId, string message)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"text", message}
            });

            return HttpClient.PostAsync<Message>(url, content)
                .ToObservable();
        }

        public IObservable<Message> UpdateMessage(string roomId, string messageId, string message)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages/{messageId}";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"text", message}
            });

            return HttpClient.PutAsync<Message>(url, content)
                .ToObservable();
        }

        #endregion

        #region Streaming

        public IObservable<Message> GetRealtimeMessages(string roomId)
        {
            string url = _baseStreamingApiAddress + $"rooms/{roomId}/chatMessages";

            return Observable.Using(() => HttpClient,
                client => client.GetStreamAsync(new Uri(url))
                    .ToObservable()
                    .Select(x => Observable.FromAsync(() => StreamHelper.ReadStreamAsync(x)).Repeat())
                    .Concat()
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(JsonConvert.DeserializeObject<Message>));
        }

        #endregion
    }
}