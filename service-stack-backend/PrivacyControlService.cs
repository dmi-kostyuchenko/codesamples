using MediatR;
using Politiq.Common.Enums;
using Politiq.Pollies.Domain.Comments;
using Politiq.Pollies.Domain.DailyVotes;
using Politiq.Pollies.Domain.ElectionComments;
using Politiq.Pollies.Domain.PartyComments;
using Politiq.Social.Domain.AudienceMembers.Queries;
using Politiq.Social.Domain.GoalComments;
using Politiq.Social.Domain.Goals;
using Politiq.Social.Domain.Goals.Enums;
using Politiq.Social.Domain.PostComments;
using Politiq.Social.Domain.Posts;
using Politiq.Social.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Politiq.Social.Services
{
    public class PrivacyControlService : IPrivacyControlService
    {
        private readonly IMediator _mediator;
        private readonly IFollowsService _followsService;

        public PrivacyControlService(IMediator mediator, IFollowsService followsService)
        {
            _mediator = mediator;
            _followsService = followsService;
        }

        public async Task<bool> CanUserCommentPost(int userId, PostState post)
        {
            var postAuthorId = int.Parse(post.AuthorId);
            return await CanUserInteract(userId, postAuthorId, post.ReplyMyPostsAndComments, post.ReplyAudiences);
        }

        public async Task<IEnumerable<Tuple<Guid, bool>>> CanUserCommentPosts(int userId, IEnumerable<PostState> posts)
        {
            var result = new List<Tuple<Guid, bool>>();

            result.AddRange(posts.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.Everyone || int.Parse(x.AuthorId) == userId)
                                 .Select(x => new Tuple<Guid, bool>(x.Id, true))
                                 .ToArray());

            var friendIds = await _followsService.GetUserFriendIds(userId);
            result.AddRange(posts.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.OnlyFriends && int.Parse(x.AuthorId) != userId)
                                 .Select(x => new Tuple<Guid, bool>(x.Id, friendIds.Contains(int.Parse(x.AuthorId))))
                                 .ToArray());

            var privatePosts = posts.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.NoOne && int.Parse(x.AuthorId) != userId);
            var audiences = await _mediator.Send(new GetAllAudiencesMembers(privatePosts.Where(x => x.ReplyAudiences != null).SelectMany(x => x.ReplyAudiences).Distinct()));

            result.AddRange(privatePosts.Select(x => new Tuple<Guid, bool>(x.Id, x.ReplyAudiences != null &&
                                                                                 audiences.Any(y => x.ReplyAudiences.Contains(y.AudienceId) && y.MemberId == userId)))
                                        .ToArray());

            return result;
        }

        public async Task<bool> CanUserCommentGoal(int userId, GoalState goal)
        {
            var audienceIds = goal.Audiences?.Where(x => x.AudienceType == GoalAudienceType.ReplyAudience).Select(x => x.AudienceId).ToArray();
            return await CanUserInteract(userId, goal.PolitiqUserId, goal.ReplyMyPostsAndComments, audienceIds);
        }

        public async Task<IEnumerable<Tuple<Guid, bool>>> CanUserCommentGoals(int userId, IEnumerable<GoalState> goals)
        {
            var result = new List<Tuple<Guid, bool>>();

            result.AddRange(goals.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.Everyone || x.PolitiqUserId == userId)
                                 .Select(x => new Tuple<Guid, bool>(x.Id, true))
                                 .ToArray());

            var friendIds = await _followsService.GetUserFriendIds(userId);
            result.AddRange(goals.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.OnlyFriends && x.PolitiqUserId != userId)
                                 .Select(x => new Tuple<Guid, bool>(x.Id, friendIds.Contains(x.PolitiqUserId)))
                                 .ToArray());

            var privateGoals = goals.Where(x => x.ReplyMyPostsAndComments == PrivacySetting.NoOne && x.PolitiqUserId != userId);
            var audiences = await _mediator.Send(new GetAllAudiencesMembers(privateGoals.Where(x => x.Audiences != null)
                                                                                        .SelectMany(x => x.Audiences.Where(y => y.AudienceType == GoalAudienceType.ReplyAudience)
                                                                                                                    .Select(y => y.AudienceId))
                                                                                        .Distinct()));

            result.AddRange(privateGoals.Select(x => new Tuple<Guid, bool>(x.Id, audiences.Any(y => x.Audiences.Any(z => z.AudienceId == y.AudienceId) && y.MemberId == userId)))
                                        .ToArray());

            return result;
        }

        public async Task<bool> CanUserSeePost(string userId, PostState post)
        {
            var postAuthorId = int.Parse(post.AuthorId);
            return await CanUserInteract(userId, postAuthorId, post.ViewMyPostsAndComments, post.ViewAudiences);
        }

        public async Task<bool> CanUserSeePostComment(string userId, PostCommentState comment)
        {
            return await CanUserInteract(userId, comment.AuthorId, comment.ViewMyPostsAndComments);
        }

        public async Task<bool> CanUserSeeGoal(string userId, GoalState goal)
        {
            var audienceIds = goal.Audiences?.Where(x => x.AudienceType == GoalAudienceType.ViewAudience).Select(x => x.AudienceId).ToArray();
            return await CanUserInteract(userId, goal.PolitiqUserId, goal.ViewMyPostsAndComments, audienceIds);
        }

        public async Task<bool> CanUserSeeGoalComment(string userId, GoalCommentState comment)
        {
            return await CanUserInteract(userId, comment.PolitiqUserId, comment.ViewMyPostsAndComments);
        }

        public async Task<bool> CanUserSeePollyComment(string userId, PollyCommentState comment)
        {
            return await CanUserInteract(userId, comment.AuthorId, comment.ViewMyPostsAndComments);
        }

        public async Task<bool> CanUserSeePartyComment(string userId, PartyCommentState comment)
        {
            return await CanUserInteract(userId, comment.AuthorId, comment.ViewMyPostsAndComments);
        }

        public async Task<bool> CanUserSeeElectionComment(string userId, ElectionCommentState comment)
        {
            return await CanUserInteract(userId, comment.AuthorId, comment.ViewMyPostsAndComments);
        }

        public async Task<bool> CanUserSeeDailyVote(string userId, DailyVoteState vote)
        {
            return await CanUserInteract(userId, vote.PolitiqUserId, vote.ViewMyVotes);
        }


        private async Task<bool> CanUserInteract(int userId, int authorId, int privacyLevel, IEnumerable<Guid> audiences = null)
        {
            if (privacyLevel == PrivacySetting.NoOne)
            {
                return userId == authorId ||
                      (audiences != null && audiences.Any() && await _mediator.Send(new IsAudiencesMember(audiences, userId)));
            }
            else if (privacyLevel == PrivacySetting.OnlyFriends)
            {
                return userId == authorId || await _followsService.IsUserFriendOfCurrent(authorId, userId);
            }

            //for privacyLevel == PrivacySetting.Everyone
            return true;
        }

        private async Task<bool> CanUserInteract(string userId, int authorId, int privacyLevel, IEnumerable<Guid> audiences = null)
        {
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int currentUserId))
            {
                //in case of anonymous user, we return true only in case of privacyLevel == PrivacySetting.Everyone (by requirements for MVP-211)
                return privacyLevel == PrivacySetting.Everyone;
            }
            else
            {
                if (privacyLevel == PrivacySetting.NoOne)
                {
                    return currentUserId == authorId ||
                          (audiences != null && audiences.Any() && await _mediator.Send(new IsAudiencesMember(audiences, currentUserId)));
                }
                else if (privacyLevel == PrivacySetting.OnlyFriends)
                {
                    return currentUserId == authorId || await _followsService.IsUserFriendOfCurrent(authorId, currentUserId);
                }

                //for privacyLevel == PrivacySetting.Everyone
                return true;
            }
        }
    }
}
