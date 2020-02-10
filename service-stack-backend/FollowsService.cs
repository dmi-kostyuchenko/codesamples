using MediatR;
using Politiq.Social.Domain.Follows;
using Politiq.Social.Domain.Follows.Queries;
using Politiq.Social.Services.Interfaces;
using Politiq.Users.Domain.UserInfo.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Politiq.Social.Services
{
    public class FollowsService : IFollowsService
    {
        private readonly IMediator _mediator;

        public FollowsService(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<IEnumerable<Tuple<string, string>>> GetFollowersDataForNotification(int userId)
        {
            var followersQuery = new GetAllUserFollowers(userId, 2); //only following closely
            var followers = await _mediator.Send(followersQuery);
            if (followers != null && followers.Any())
            {
                var followerUsers = await _mediator.Send(new GetUsersPrivateInfoBySetOfIds(followers.Select(x => x.UserId).ToArray()));

                if (followerUsers != null && followerUsers.Any())
                    return followerUsers.Select(x => new Tuple<string, string>(x.Email, x.DisplayName)).ToArray();
            }

            return Array.Empty<Tuple<string, string>>();
        }

        public async Task<FollowState> GetFollowState(int followeeId, int userId, string followeeType)
        {
            var query = new GetFollowState(followeeId, userId, followeeType);
            return await _mediator.Send(query);
        }

        public async Task<IEnumerable<FollowState>> GetFollowStates(IEnumerable<int> followeeIds, int userId, string followeeType)
        {
            var query = new GetFollowStates(followeeIds, userId, followeeType);
            return await _mediator.Send(query);
        }

        public async Task<IEnumerable<int>> GetUserFriendIds(int userId)
        {
            var followeesQuery = new GetAllUserFriends(userId);
            var followees = await _mediator.Send(followeesQuery);
            return followees != null && followees.Any() ? followees.Select(x => x.FolloweeId).ToArray() : Array.Empty<int>();
        }

        public async Task<IEnumerable<int>> GetUserFriendIds(string userId) =>
            int.TryParse(userId, out int id) ? await GetUserFriendIds(id) : null;

        public async Task<bool> IsUserFriendOfCurrent(int userId, int currentUserId)
        {
            var followees = await _mediator.Send(new GetAllUserFriends(currentUserId));
            return followees != null && followees.Any(x => x.FolloweeId == userId);
        }

    }
}
