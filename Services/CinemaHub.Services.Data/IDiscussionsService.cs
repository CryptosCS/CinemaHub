﻿namespace CinemaHub.Services.Data
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using CinemaHub.Web.ViewModels.Discussions;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Routing;

    public interface IDiscussionsService
    {
        Task<IEnumerable<T>> GetDiscussions<T>(string mediaId, int page, int count);

        Task<IEnumerable<T>> GetComments<T>(string discussionId, int page, int count);

        Task CreateDiscussion(DiscussionInputModel inputModel, string userId);

        Task CreateComment(string inputModel, string userId, string discussionId);

        Task DeleteComment(string commentId);

        Task DeleteDiscussion(string discussionId);

        Task<int> GetTotalDiscussions(string mediaId);

        Task<int> GetTotalComments(string discussionId);

        Task<string> GetDiscussionTitle(string discussionId);

        Task<string> GetDiscussionMedia(string discussionId);

        Task<T> GetDiscussionInfo<T>(string discussionId);

        Task<T> GetCommentInfo<T>(string commentId);
    }
}