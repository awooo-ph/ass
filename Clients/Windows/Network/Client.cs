﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.DPSBase;
using NetworkCommsDotNet.Tools;

namespace norsu.ass.Network
{
    class Client
    {
        private Client()
        {
            if (_started)
                return;
            _started = true;

            //  NetworkComms.DisableLogging();
            NetworkComms.EnableLogging();

            NetworkComms.IgnoreUnknownPacketTypes = true;
            var serializer = DPSManager.GetDataSerializer<NetworkCommsDotNet.DPSBase.ProtobufSerializer>();

            NetworkComms.DefaultSendReceiveOptions = new SendReceiveOptions(serializer,
                NetworkComms.DefaultSendReceiveOptions.DataProcessors, NetworkComms.DefaultSendReceiveOptions.Options);

            NetworkComms.AppendGlobalIncomingPacketHandler<ServerInfo>(ServerInfo.Header, ServerInfoReceived);
            NetworkComms.AppendGlobalIncomingPacketHandler<UserPicture>(UserPicture.Header,
                (h, c, res) =>AddPicture(res));
            NetworkComms.AppendGlobalIncomingPacketHandler<OfficePicture>(OfficePicture.Header,
                (h, c, res) => AddOfficePicture(res));
            NetworkComms.AppendGlobalIncomingPacketHandler<Shutdown>(Shutdown.Header, ShutdownHandler);
            
            PeerDiscovery.EnableDiscoverable(PeerDiscovery.DiscoveryMethod.UDPBroadcast);

            PeerDiscovery.OnPeerDiscovered += OnPeerDiscovered;
            Connection.StartListening(ConnectionType.UDP, new IPEndPoint(IPAddress.Any, 0));

            PeerDiscovery.DiscoverPeersAsync(PeerDiscovery.DiscoveryMethod.UDPBroadcast);
        }
        
        private void ShutdownHandler(PacketHeader packetHeader, Connection connection, Shutdown incomingObject)
        {
            Server = null;
            Messenger.Default.Broadcast(Messages.ServerShutdown);
        }

        public static async Task<GetUsersResult> GetUsers(int page)
        {
            if (Server == null) await FindServer();
            if (Server == null) return null;

            GetUsersResult result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<GetUsersResult>(GetUsersResult.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(GetUsersResult.Header);
                    result = res;
                });

            await new GetUsers()
            {
                Page = page,
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        private readonly List<OfficePicture> OfficePictures = new List<OfficePicture>();
        private void AddOfficePicture(OfficePicture picture)
        {
            while (true)
            {
                try
                {
                    var pic = OfficePictures.FirstOrDefault(x => x.OfficeId == picture.OfficeId);
                    if (pic == null)
                    {
                        OfficePictures.Add(picture);
                    }
                    else
                    {
                        pic.Picture = pic.Picture;
                    }
                    Messenger.Default.Broadcast(Messages.OfficePictureReceived, pic);
                    return;
                }
                catch (Exception e)
                {
                    //
                }
            }
        }

        public static OfficePicture GetOfficePicture(long officeId)
        {
            while (true)
            {
                try
                {
                    return Instance.OfficePictures.FirstOrDefault(x => x.OfficeId == officeId);
                }
                catch (Exception e)
                {
                    //
                }
            }
        }

        ~Client()
        {
            Stop();
        }

        private static Client _instance;
        public static Client Instance => _instance ?? (_instance = new Client());
        
        private static bool _started = false;

        public static void Start()
        {
            Instance._Start();
        }
        
        private void _Start()
        {
           
        }

        public static void Stop()
        {
            Connection.StopListening();
            NetworkComms.Shutdown();
        }

        private static ServerInfo _server;

        public static ServerInfo Server
        {
            get
            {
                return _server;
            }
            set
            {
                _server = value;
            }
        }

        public static Office SelectedOffice { get; set; }

        private void ServerInfoReceived(PacketHeader packetheader, Connection connection, ServerInfo incomingobject)
        {
            Server = incomingobject;
            Messenger.Default.Broadcast(Messages.ServerFound);
        }

        private async void OnPeerDiscovered(ShortGuid peeridentifier, Dictionary<ConnectionType, List<EndPoint>> endPoints)
        {
            var info = new Desktop();

            var eps = endPoints[ConnectionType.UDP];
            var localEPs = Connection.AllExistingLocalListenEndPoints();

            foreach(var value in eps)
            {
                var ip = value as IPEndPoint;
                if(ip?.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                foreach(var localEP in localEPs[ConnectionType.UDP])
                {
                    var lEp = (IPEndPoint)localEP;
                    if(!ip.Address.IsInSameSubnet(lEp.Address))
                        continue;
                    info.IP = lEp.Address.ToString();
                    info.Port = lEp.Port;
                    await info.Send(ip);
                }
            }
        }

        private static bool _findStarted;
        public static async Task FindServer()
        {
            if (_findStarted) return;
            _findStarted = true;
            await Instance._FindServer();
            _findStarted = false;
        }
        
        private async Task _FindServer()
        {
            var start = DateTime.Now;
            PeerDiscovery.DiscoverPeersAsync(PeerDiscovery.DiscoveryMethod.UDPBroadcast);
            while ((DateTime.Now - start).TotalSeconds < 7)
            {
                if (Server != null)
                    break;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
        }

        public int Session { get; set; }

        public static async Task<DesktopLoginResult> Login(string username, string password)
        {
            return await Instance._Login(username, password);
        }
        
        private async Task<DesktopLoginResult> _Login(string username, string password, bool annonymous = false)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;

            DesktopLoginResult result = null;
            NetworkComms.AppendGlobalIncomingPacketHandler<DesktopLoginResult>(DesktopLoginResult.Header,
                (h, c, r) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(DesktopLoginResult.Header);
                    result = r;
                });
            
            await new DesktopLoginRequest()
            {
                Username = username,
                Password = password, //very secure :D
            }.Send(Server.IP,Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public async void FetchOfficePicture(long id)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return;
            if (OfficePictures.Any(x => x.OfficeId == id)) return;
            await new GetOfficePicture() {Session = Session, OfficeId = id}.Send(Server.IP, Server.Port);
        }

        public async void FetchOfficePictures(IEnumerable<long> officeIds)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return;

            try
            {
                foreach (var id in officeIds)
                {
                    if (OfficePictures.Any(x => x.OfficeId == id))
                        continue;
                    await new GetOfficePicture() {Session = Session, OfficeId = id}
                        .Send(Server.IP, Server.Port);
                }
            }
            catch (Exception e)
            {
                //
            }

        }

        public static async Task<Offices> GetOffices()
        {
            return await Instance._GetOffices();
        }
        private async Task<Offices> _GetOffices()
        {
            if (Server == null) await _FindServer();
            if (Server == null) return null;

            Offices result = null;
            NetworkComms.AppendGlobalIncomingPacketHandler<Offices>(Offices.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(Offices.Header);
                    result = res;
                });

            await Packet.Send(Requests.GET_OFFICES,Server.IP,Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<OfficeRatings> GetRatings(long officeId, int page)
        {
            //var ratings = OfficeRatingCache.FirstOrDefault(x => x.OfficeId == officeId && x.Page == page);
            //if (ratings != null) return ratings;
            
            return await Instance._GetRatings(officeId,page);
        }
        private static List<OfficeRatings> OfficeRatingCache { get; } = new List<OfficeRatings>();
        private async Task<OfficeRatings> _GetRatings(long officeId,int page)
        {
            
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;
            
            OfficeRatings result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<OfficeRatings>(OfficeRatings.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(OfficeRatings.Header);
                    result = res;

                    //var ratings = OfficeRatingCache.FirstOrDefault(x => x.OfficeId == officeId && x.Page == page);
                    //if (ratings == null) OfficeRatingCache.Add(res);
                    
                    var rating = res.Ratings.FirstOrDefault(x => x.MyRating);
                    if(rating!=null) AddRating(rating.OfficeId,rating);
                    
                    FetchUserPictures(res.Ratings.Select(x=>x.UserId));
                });

            await new GetRatings()
            {
                OfficeId = officeId,
                Session = Session,
                Page = page
            }.Send(Server.IP, Server.Port);
            
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<RateOfficeResult> RateOffice(long officeId, int rating, string message, bool isPrivate, long returnCount = -1)
        {
            return await Instance._RateOffice(officeId, rating, message, isPrivate, returnCount);
        }
        private async Task<RateOfficeResult> _RateOffice(long officeId, int rating, string message, bool isPrivate = false, long returnCount = -1)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;

            RateOfficeResult result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<RateOfficeResult>(RateOfficeResult.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(RateOfficeResult.Header);
                    result = res;
                });
            
            await new RateOffice()
            {
                IsPrivate = isPrivate,
                Message = message,
                OfficeId = officeId,
                Rating = rating,
                Session = Session,
                ReturnCount = returnCount,
            }.Send(Server.IP, Server.Port);
            
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<Suggestions> GetSuggestions(long officeId, int page)
        {
            return await Instance._GetSuggestions(officeId,page);
        }

        private async Task<Suggestions> _GetSuggestions(long officeId, int page)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;

            Suggestions result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<Suggestions>(Suggestions.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(Suggestions.Header);
                    result = res;
                    FetchUserPictures(res.Items.Select(x=>x.UserId));
                });

            await new GetSuggestions()
            {
                OfficeId = officeId,
                Session = Session,
                Page = page,
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        private async void FetchUserPicture(long id)
        {
            if (Server == null) await _FindServer();
            if (Server == null) return;
            if (Pictures.Any(x => x.UserId == id)) return;
            await new GetPicture() {Session = Session, UserId = id}.Send(Server.IP, Server.Port);
        }
        private async void FetchUserPictures(IEnumerable<long> userIds)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null) return;
           
            try
            {
                foreach (var id in userIds)
                {
                    if(Pictures.Any(x=>x.UserId==id)) continue;
                    await new GetPicture() {Session = Session, UserId = id}
                        .Send(Server.IP,Server.Port);
                }
            }
            catch (Exception e)
            {
               //
            }
            
        }
        
        public static Suggestion SelectedSuggestion { get; set; }

        public static async Task<SuggestResult> Suggest(long officeId, string subject, string body,bool isPrivate)
        {
            return await Instance._Suggest(officeId, subject, body, isPrivate);
        }
        
        private async Task<SuggestResult> _Suggest(long officeId, string subject, string body,bool isPrivate)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;

            SuggestResult result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<SuggestResult>(SuggestResult.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(SuggestResult.Header);
                    result = res;
                });

            await new Suggest()
            {
                OfficeId = officeId,
                Session = Session,
                Subject = subject,
                Body = body,
                IsPrivate = isPrivate
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<long?> LikeSuggestion(long suggestionId, bool dislike)
        {
            return await Instance._LikeSuggestion(suggestionId, dislike);
        }
        private async Task<long?> _LikeSuggestion(long suggestionId, bool dislike)
        {
            if (Server == null) await _FindServer();
            if (Server == null) return null;

            long? result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<long>(Requests.LIKE_SUGGESTION,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(Requests.LIKE_SUGGESTION);
                    result = res;
                });

            await new LikeSuggestion()
            {
                SuggestionId = suggestionId,
                Dislike = dislike,
                Session = Session,
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<Comments> GetComments(long suggestionId)
        {
            return await Instance._GetComments(suggestionId);
        }
        private async Task<Comments> _GetComments(long suggestionId)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return null;

            Comments result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<Comments>(Comments.Header,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(Comments.Header);
                    result = res;
                    FetchUserPictures(res.Items.Select(x => x.UserId));
                });

            await new GetComments()
            {
                SuggestionId = suggestionId,
                Session = Session,
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return null;
        }

        public static async Task<bool> AddComment(long suggestionId, string comment)
        {
            return await Instance._AddComment(suggestionId, comment);
        }
        private async Task<bool> _AddComment(long suggestionId, string comment)
        {
            if (Server == null)
                await _FindServer();
            if (Server == null)
                return false;

            bool? result = null;

            NetworkComms.AppendGlobalIncomingPacketHandler<bool>(Requests.ADD_COMMENT,
                (h, c, res) =>
                {
                    NetworkComms.RemoveGlobalIncomingPacketHandler(Requests.ADD_COMMENT);
                    result = res;
                });

            await new AddComment()
            {
                SuggestionId = suggestionId,
                Session = Session,
                Message = comment
            }.Send(Server.IP, Server.Port);

            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < 17)
            {
                if (result != null)
                    return result??false;
                await TaskEx.Delay(TimeSpan.FromSeconds(1));
            }
            Server = null;
            return false;

        }

        private readonly List<UserPicture> Pictures = new List<UserPicture>();

        private void AddPicture(UserPicture picture)
        {
            while (true)
            {
                try
                {
                    var pic = Pictures.FirstOrDefault(x => x.UserId == picture.UserId);
                    if (pic == null)
                    {
                        Pictures.Add(picture);
                    }
                    else
                    {
                        pic.Picture = pic.Picture;
                    }
                    Messenger.Default.Broadcast(Messages.PictureReceived, pic);
                    return;
                }
                catch (Exception e)
                {
                    //
                }
            }
        }

        public static UserPicture GetPicture(long userId)
        {
            while (true)
            {
                try
                {
                    var pic = Instance.Pictures.FirstOrDefault(x => x.UserId == userId);
                    if (pic != null) return pic;
                    break;
                }
                catch (Exception e)
                {
                    //
                }
            }
            return null;
        }

        private Dictionary<long,OfficeRating> MyRatings = new Dictionary<long, OfficeRating>();

        private void AddRating(long id, OfficeRating rating)
        {
            if (MyRatings.ContainsKey(id))
                MyRatings[id] = rating;
            else
                MyRatings.Add(id,rating);
        }
        
        public static OfficeRating GetMyRating(long id)
        {
            return Instance._GetMyRating(id);
        }

        private OfficeRating _GetMyRating(long id)
        {
            while (true)
            {
                try
                {
                    return MyRatings[id];
                }
                catch (Exception e)
                {
                    //
                }
            }
        }
    }
}