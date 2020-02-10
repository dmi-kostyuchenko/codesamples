using Hangfire;
using MediatR;
using Politiq.Social.Domain.PostComments.Notifications;
using Politiq.Social.Domain.PostComments.Queries;
using Politiq.Social.Domain.Posts.Queries;
using Politiq.Social.Services.Interfaces;
using Politiq.Social.Services.Utils;
using Politiq.Users.Domain.UserInfo;
using Politiq.Users.Domain.UserInfo.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Politiq.Social.Services
{
    public class MentionsProcessingService : IMentionsProcessingService
    {
        private readonly IMediator _mediator;

        public MentionsProcessingService(IMediator mediator)
        {
            _mediator = mediator;
        }

        [DisableConcurrentExecution(300)]
        public async Task ProcessMentionsInPostComment(Guid commentId)
        {
            var query = new GetCommentById(commentId);
            var comment = await _mediator.Send(query);
            if (comment == null) return;

            var mentions = MentionsHelper.FindMentions(comment.Body);
            var users = await GetMentionedUsers(mentions, comment.MentionedUsers, comment.AuthorId);
            if (users == null || !users.Any()) return;

            //we are getting the post here to be sure the slug not changed
            var postQuery = new GetPostById(comment.PostId);
            var post = await _mediator.Send(postQuery);

            var user = await _mediator.Send(new GetUserInfoById(comment.AuthorId));

            var notification = new PostCommentMentionNotification(post.Title, user.DisplayName, post.Slug, users.Select(x => new Tuple<string, string>(x.Email, x.DisplayName)));
            await _mediator.Publish(notification);
            //update mentioned users
            var updateMentionedQuery = new Domain.PostComments.Commands.UpdateMentionedUsers(comment.Id, mentions);
            await _mediator.Send(updateMentionedQuery);
        }

        [DisableConcurrentExecution(300)]
        public async Task ProcessMentionsInPost(Guid postId)
        {
            var post = await _mediator.Send(new GetPostById(postId));
            if (post == null || !int.TryParse(post.AuthorId, out int authorId)) return;

            var mentions = MentionsHelper.FindMentions(post.Body);
            var users = await GetMentionedUsers(mentions, post.MentionedUsers, authorId);
            if (users == null || !users.Any()) return;

            var user = await _mediator.Send(new GetUserInfoById(authorId));

            var notification = new PostCommentMentionNotification(post.Title, user.DisplayName, post.Slug, users.Select(x => new Tuple<string, string>(x.Email, x.DisplayName)));
            await _mediator.Publish(notification);

            //update mentioned users
            var updateMentionedQuery = new Domain.Posts.Commands.UpdateMentionedUsers(post.Id, mentions);
            await _mediator.Send(updateMentionedQuery);
        }
        
        private async Task<IEnumerable<UserPrivateInfoState>> GetMentionedUsers(IEnumerable<string> newMentions, IEnumerable<string> oldMentions, int authorId)
        {
            //filter previously mentioned users
            if (oldMentions != null && oldMentions.Any())
                newMentions = newMentions.Where(x => !oldMentions.Any(y => string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase))).ToArray();

            //get users
            var usersQuery = new GetUserPrivateInfoBySetOfNames(newMentions);
            var users = await _mediator.Send(usersQuery);
            
            //exclude self
            if (users != null && users.Any())
                users = users.Where(x => x.Id != authorId).ToArray();

            return users;
        }
    }
}
