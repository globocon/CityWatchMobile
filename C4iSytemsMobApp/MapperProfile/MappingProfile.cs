using AutoMapper;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Models;


namespace C4iSytemsMobApp.MapperProfile
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<PatrolCarLog, PatrolCarLogRequestCache>()
                // Direct mappings
                .ForMember(dest => dest.PatrolCarId,
                            opt => opt.MapFrom(src => src.PatrolCarId))

                .ForMember(dest => dest.ClientSiteLogBookId,
                            opt => opt.MapFrom(src => src.ClientSiteLogBookId))

                .ForMember(dest => dest.Mileage,
                            opt => opt.MapFrom(src => src.Mileage))

                .ForMember(dest => dest.MileageText,
                            opt => opt.MapFrom(src => src.MileageText))

                .ForMember(dest => dest.PatrolCar,
                            opt => opt.MapFrom(src => src.PatrolCar))

                // ---- Nested navigation flattening ----
                .ForMember(dest => dest.Model,
                            opt => opt.MapFrom(src => src.ClientSitePatrolCar.Model))

                .ForMember(dest => dest.Rego,
                            opt => opt.MapFrom(src => src.ClientSitePatrolCar.Rego))

                .ForMember(dest => dest.ClientSiteId,
                            opt => opt.MapFrom(src => src.ClientSitePatrolCar.ClientSiteId))

                // Fields that don't exist in source — ignore or fill later
                .ForMember(dest => dest.CacheId, opt => opt.Ignore())
                .ForMember(dest => dest.SiteId, opt => opt.Ignore())
                .ForMember(dest => dest.EventDateTimeLocal, opt => opt.MapFrom(src => TimeZoneHelper.GetCurrentTimeZoneCurrentTime()))
                .ForMember(dest => dest.EventDateTimeLocalWithOffset, opt => opt.MapFrom(src => TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset()))
                .ForMember(dest => dest.EventDateTimeZone, opt => opt.MapFrom(src => TimeZoneHelper.GetCurrentTimeZone()))
                .ForMember(dest => dest.EventDateTimeZoneShort, opt => opt.MapFrom(src => TimeZoneHelper.GetCurrentTimeZoneShortName()))
                .ForMember(dest => dest.EventDateTimeUtcOffsetMinute, opt => opt.MapFrom(src => TimeZoneHelper.GetCurrentTimeZoneOffsetMinute()))
                .ForMember(dest => dest.IsSynced, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.UniqueRecordId, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.DeviceId, opt => opt.Ignore())
                .ForMember(dest => dest.DeviceName, opt => opt.Ignore());

            // ----------------------------
            // ClientSitePatrolCar <-> Cache
            // ----------------------------
            CreateMap<ClientSitePatrolCar, ClientSitePatrolCarCache>()
                .ForMember(dest => dest.PatrolCarLog, opt => opt.Ignore());
            // Avoid circular reference mapping

            CreateMap<ClientSitePatrolCarCache, ClientSitePatrolCar>();

            // ----------------------------
            // PatrolCarLog <-> Cache
            // ----------------------------
            CreateMap<PatrolCarLog, PatrolCarLogCache>()
                .ForMember(dest => dest.ClientSitePatrolCar,
                           opt => opt.MapFrom(src => src.ClientSitePatrolCar));

            CreateMap<PatrolCarLogCache, PatrolCarLog>()
                .ForMember(dest => dest.ClientSitePatrolCar,
                           opt => opt.MapFrom(src => src.ClientSitePatrolCar));
        }
    }
}
