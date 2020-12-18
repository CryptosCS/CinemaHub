﻿namespace CinemaHub.Services.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    using AutoMapper;
    using CinemaHub.Common;
    using CinemaHub.Data;
    using CinemaHub.Data.Common.Repositories;
    using CinemaHub.Data.Models;
    using CinemaHub.Data.Models.Enums;
    using CinemaHub.Services;
    using CinemaHub.Services.Data.Models;
    using CinemaHub.Services.Mapping;
    using CinemaHub.Web.ViewModels.Media;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    public class MediaService : IMediaService
    {
        private readonly IRepository<Media> mediaRepository;

        private readonly IRepository<Genre> genreRepository;

        private readonly IRepository<Keyword> keywordRepository;

        private readonly IRepository<MediaKeyword> mediaKeywordRepository;

        public MediaService(
            IRepository<Media> mediaRepository,
            IRepository<Genre> genreRepository,
            IRepository<Keyword> keywordRepository,
            IRepository<MediaKeyword> mediaKeywordRepository)
        {
            this.mediaRepository = mediaRepository;
            this.genreRepository = genreRepository;
            this.keywordRepository = keywordRepository;
            this.mediaKeywordRepository = mediaKeywordRepository;
        }

        public async Task<MediaResultDTO> GetPageAsync(MediaQueryDTO query)
        {
            int paginationCount = (query.Page - 1) * query.ElementsPerPage;

            var mediaQuery = this.mediaRepository.AllAsNoTracking();

            var filteredQuery = ApplyQuery(mediaQuery, query);

            var resultsFound = await filteredQuery.CountAsync();

            var results = await filteredQuery.ToListAsync();

            var medias = await filteredQuery.Skip(paginationCount).Take(query.ElementsPerPage).Select(
                             x => new MediaGridDTO()
                                      {
                                          Id = x.Id,
                                          IsDetailFull = x.IsDetailFull,
                                          MovieApiId = x.MovieApiId,
                                          Overview = x.Overview,
                                          Title = x.Title,
                                          ImagePath =
                                              x.Images.FirstOrDefault(x => x.ImageType == ImageType.Poster).Path,
                                          MediaType = x.GetType().Name,
                                          Rating = x.Ratings.Select(x => x.Score).Average(x => x),
                                      }).ToListAsync();

            return new MediaResultDTO()
                       {
                           ResultCount = resultsFound,
                           Results = medias,
                           ResultsPerPage = query.ElementsPerPage,
                           CurrentPage = query.Page,
                       };
        }

        public async Task<T> GetDetailsAsync<T>(string id)
        {
            var media = await this.mediaRepository.AllAsNoTracking()
                .Where(x => x.Id == id)
                .To<T>().FirstOrDefaultAsync();

            return media;
        }

        public async Task EditDetailsAsync(MediaDetailsInputModel inputModel, string userId, string rootPath)
        {
            var media = this.mediaRepository.All()
                .Include(x => x.Genres)
                .ThenInclude(x => x.Genre)
                .Include(x => x.Keywords)
                .Include(x => x.Images)
                .FirstOrDefault(x => x.Id == inputModel.Id);

            bool isAdd = false;

            // check if the type we try to add create exists
            if (media == null)
            {
                isAdd = true;
                string qualifiedName =
                    $"CinemaHub.Data.Models.{inputModel.MediaType}, CinemaHub.Data.Models, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

                try
                {
                    var type = Type.GetType(qualifiedName);
                    media = (Media)Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    throw new Exception($"{inputModel.MediaType} does not exist");
                }
            }

            // if the media exists replace all its fields, if it does not set the fields
            media.Title = inputModel.Title ?? media.Title;
            media.Overview = inputModel.Overview ?? media.Overview;
            media.IsDetailFull = true;
            media.Budget = inputModel.Budget;
            media.Language = inputModel.Language ?? media.Language;
            media.Runtime = inputModel.Runtime;
            media.YoutubeTrailerUrl = inputModel.YoutubeTrailerUrl ?? media.YoutubeTrailerUrl;
            media.ReleaseDate = inputModel.ReleaseDate;

            var genres = inputModel.Genres.Split(new string[] { ", ", "&" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();
            var genresDb = media.Genres.ToList();

            // Remove all genres which are not in the inputModel's genres and ignore all which are
            foreach (var genre in genresDb)
            {
                if (!genres.Contains(genre.Genre.Name))
                {
                    media.Genres.Remove(genre);
                    continue;
                }

                genres.Remove(genre.Genre.Name);
            }

            // Add all the remaining genres which remained in [genres]
            foreach (var genre in genres)
            {
                var genreDb = this.genreRepository.All().FirstOrDefault(x => x.Name == genre);
                if (genreDb != null)
                {
                    media.Genres.Add(
                        new MediaGenre()
                            {
                                Media = media,
                                Genre = genreDb,
                            });
                }
            }

            // If there are no keywords everything will be removed
            var keywords = inputModel.Keywords != null
                               ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<KeywordDTO>>(inputModel.Keywords)
                               : new List<KeywordDTO>();

            var dbKeywordsIds = media.Keywords.Select(x => x.KeywordId).ToList();

            // if [keyword.Id] is 0 it does not exist.
            foreach (var keyword in keywords)
            {
                if (keyword.Id == 0)
                {
                    var keywordDb = new Keyword() { Name = keyword.Value };
                    await this.keywordRepository.AddAsync(keywordDb);

                    media.Keywords.Add(new MediaKeyword() { Keyword = keywordDb, Media = media, });
                    continue;
                }

                if (!dbKeywordsIds.Contains(keyword.Id))
                {
                    media.Keywords.Add(new MediaKeyword() { KeywordId = keyword.Id, Media = media, });
                }

                dbKeywordsIds.Remove(keyword.Id); // Keywords which are removed from [dbKeywordsIds] won't be deleted
            }

            // Delete all the keywords which have remained in [dbKeywordsIds]
            foreach (var id in dbKeywordsIds)
            {
                var mediaKeyword = media.Keywords.FirstOrDefault(x => x.KeywordId == id);
                this.mediaKeywordRepository.Delete(mediaKeyword);
            }

            // Add/Replaces image poster
            var image = inputModel.PosterImageFile;
            if (image != null)
            {
                try
                {
                    var mediaImage = await this.DownloadPosterImage(image, rootPath, media);

                    var posterImage = media.Images.FirstOrDefault(x => x.ImageType == ImageType.Poster);
                    if (posterImage != null)
                    {
                        media.Images.Remove(posterImage);
                    }
                    media.Images.Add(mediaImage);

                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            // [isAdd] is set to true if [inputModel.Id] is null (creating new media)
            if (isAdd)
            {
                await this.mediaRepository.AddAsync(media);
            }
            await this.mediaRepository.SaveChangesAsync();
        }

        public string IsMediaDetailsFullAsync(string id)
        {
            try
            {
                var media = this.mediaRepository.All().Where(x => x.Id == id).FirstOrDefault();
                if (!media.IsDetailFull)
                {
                    return media.Title;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                throw new Exception($"Id {id} does not exist");
            }
        }

        public async Task<IEnumerable<T>> GetMediaBatch<T>(IEnumerable<string> mediaIds)
        {
            return await this.mediaRepository.AllAsNoTracking()
                       .Where(x => mediaIds.Contains(x.Id))
                       .To<T>()
                       .ToListAsync();
        }

        private async Task<MediaImage> DownloadPosterImage(IFormFile image, string rootPath, Media media)
        {
            Directory.CreateDirectory($"{rootPath}/images/posters/");

            // Check if extension is allowed

            var extension = Path.GetExtension(image.FileName).TrimStart('.');

            var imageFileSignatures = GlobalConstants.ImageFileSignatures;

            if (!imageFileSignatures.Any(x => x.Key == extension))
            {
                throw new Exception($"Invalid image extension {extension}");
            }

            // Check file's header bytes for the given extension's file signature
            using (Stream readStream = image.OpenReadStream())
            {
                using var binaryReader = new BinaryReader(readStream);

                var signatures = imageFileSignatures[extension];
                var headerBytesToCheck = signatures.Max(x => x.Length);

                var headerBytes = binaryReader.ReadBytes(headerBytesToCheck);

                bool isValidImage = signatures.Any(signatureBytes =>
                    headerBytes.Take(signatureBytes.Length)
                    .SequenceEqual(signatureBytes));

                if (!isValidImage)
                {
                    throw new Exception($"Invalid file format");
                }
            }

            // Creates the image and returns the db entity binded to it
            var imageDb = new MediaImage()
                                   {
                                       MediaId = media.Id,
                                       ImageType = ImageType.Poster,
                                       Path = $"\\images\\posters\\poster-{media.Id}.{extension}",
                                       Extension = extension,
                                       Title = media.Title + " - Poster",
                                   };

            var physicalPath = rootPath + imageDb.Path;
            using (var fileStream = new FileStream(physicalPath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            return imageDb;
        }

        private static IQueryable<Media> ApplyQuery(IQueryable<Media> source, MediaQueryDTO query)
        {
            if (query.MediaType == nameof(Movie))
            {
                source = source.OfType<Movie>();
            }
            else if (query.MediaType == nameof(Show))
            {
                source = source.OfType<Show>();
            }

            if (!string.IsNullOrWhiteSpace(query.Keywords))
            {
                var keywords = query.Keywords.Split(", ").Select(x => int.Parse(x)).ToList();
                source = source.Where(x =>
                    x.Keywords.Any(y =>
                        keywords.Contains(y.KeywordId)));
            }

            if (!string.IsNullOrWhiteSpace(query.Genres))
            {
                var genres = query.Genres.Split(", ");

                foreach(string genre in genres)
                {
                    source = source.Where(x => x.Genres.Select(x => x.Genre.Name).Contains(genre));
                }
            }

            if(!string.IsNullOrWhiteSpace(query.WatchType))
            {
                var isValidWatchType = Enum.TryParse<WatchType>(query.WatchType, true, out WatchType watchType);
                if (!isValidWatchType)
                {
                    throw new InvalidEnumArgumentException($"Watchtype {query.WatchType} is not valid!");
                }

                source = source.Where(
                    x => x.Watchers.Any(x => x.UserId == query.UserId && x.WatchType == watchType));
            }

            if (!query.AreNotWatched)
            {
                source = source.Where(x => x.Watchers.Any(x => x.UserId != query.UserId));
            }

            if (query.SearchQuery != null)
            {
                source = source.Where(x =>
                    x.Title.Contains(query.SearchQuery ?? string.Empty));
            }

            source = query.SortType switch
                {
                    "popularity" => source.Include(x => x.Watchers).OrderBy(x => x.Watchers.Count()),
                    "popularity-desc" => source.Include(x => x.Watchers).OrderByDescending(x => x.Watchers.Count()),
                    "rating" => source.OrderBy(x => x.Ratings.Average(x => x.Score)),
                    "rating-desc" => source.OrderByDescending(x => x.Ratings.Average(x => x.Score)),
                    "date" => source.OrderBy(x => x.ReleaseDate),
                    "date-desc" => source.OrderByDescending(x => x.ReleaseDate),
                    _ => source.OrderBy(x => x.Title),
                };

            return source;
        }
    }
}
