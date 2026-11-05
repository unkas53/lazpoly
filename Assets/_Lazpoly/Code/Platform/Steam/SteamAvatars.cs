using System.Collections.Generic;
using Lazpoly.App.Avatars;
using Steamworks;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// Steam profile pictures (the large, 184 pixel ones). Steam hands them over as an image handle once it has
    /// them: 0 means it has nothing yet (the user's details are asked for), -1 that it is fetching; either way the
    /// caller asks again later. Each picture is read once and kept.
    /// </summary>
    public sealed class SteamAvatars : IPlayerAvatars
    {
        private const int NoImage = 0;
        private const int Loading = -1;

        private readonly SteamService _steam;
        private readonly Dictionary<ulong, AvatarPicture> _pictures = new Dictionary<ulong, AvatarPicture>();
        private readonly HashSet<ulong> _asked = new HashSet<ulong>();

        public SteamAvatars(SteamService steam)
        {
            _steam = steam;
        }

        public AvatarPicture Get(ulong platformId)
        {
            if (platformId == 0 || _steam == null || !_steam.IsInitialized) return null;
            if (_pictures.TryGetValue(platformId, out AvatarPicture kept)) return kept;

            var id = new CSteamID(platformId);
            int handle = SteamFriends.GetLargeFriendAvatar(id);
            if (handle == Loading) return null;
            if (handle == NoImage)
            {
                // Someone met only in a lobby: their details (picture included) come after they are asked for.
                if (_asked.Add(platformId)) SteamFriends.RequestUserInformation(id, false);
                return null;
            }

            if (!SteamUtils.GetImageSize(handle, out uint width, out uint height) || width == 0 || height == 0) return null;
            var rgba = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(handle, rgba, rgba.Length)) return null;
            var picture = new AvatarPicture((int)width, (int)height, rgba);
            _pictures[platformId] = picture;
            return picture;
        }
    }
}
