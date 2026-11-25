using Microsoft.EntityFrameworkCore;
using YEAPI.DTO;
using YEAPI.Models;

namespace YEAPI.Extensions
{
    public static class YEGroupExtensions
    {
        public static IQueryable<YEGroupDto> WithoutImage(this IQueryable<YEGroup> query)
        {
            return query.Select(x => new YEGroupDto
            {
                Id = x.Id,
                YaName = x.YaName,
                ImgUpdated = x.ImgUpdated,
                Actual = x.Actual
            });
        }
    }
}