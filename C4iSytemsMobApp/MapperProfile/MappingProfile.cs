using AutoMapper;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Models;
using static C4iSytemsMobApp.WebIncidentReport;


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

            // ----------------------------
            // ClientSiteTypeLocal <-> Cache
            // ----------------------------
            CreateMap<DropdownItem, ClientSiteTypeLocal>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.ClientSites, opt => opt.Ignore());

            CreateMap<ClientSiteTypeLocal, DropdownItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.TypeName));

            CreateMap<ClientSitesLocal, ClientSite>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.TypeId, opt => opt.MapFrom(src => src.TypeId))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State))
                .ForMember(dest => dest.Gps, opt => opt.MapFrom(src => src.Gps))
                .ForMember(dest => dest.Billing, opt => opt.MapFrom(src => src.Billing))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.StatusDate, opt => opt.MapFrom(src => src.StatusDate))
                .ForMember(dest => dest.SiteEmail, opt => opt.MapFrom(src => src.SiteEmail))
                .ForMember(dest => dest.LandLine, opt => opt.MapFrom(src => src.LandLine))
                .ForMember(dest => dest.DuressEmail, opt => opt.MapFrom(src => src.DuressEmail))
                .ForMember(dest => dest.DuressSms, opt => opt.MapFrom(src => src.DuressSms))
                .ForMember(dest => dest.UploadGuardLog, opt => opt.MapFrom(src => src.UploadGuardLog))
                .ForMember(dest => dest.UploadFusionLog, opt => opt.MapFrom(src => src.UploadFusionLog))
                .ForMember(dest => dest.GuardLogEmailTo, opt => opt.MapFrom(src => src.GuardLogEmailTo))
                .ForMember(dest => dest.DataCollectionEnabled, opt => opt.MapFrom(src => src.DataCollectionEnabled))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.IsDosDontList, opt => opt.MapFrom(src => src.IsDosDontList))
                .ForMember(dest => dest.MobAppShowClientTypeandSite, opt => opt.MapFrom(src => src.MobAppShowClientTypeandSite));

            CreateMap<ClientSite, ClientSitesLocal>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.TypeId, opt => opt.MapFrom(src => src.TypeId))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State))
                .ForMember(dest => dest.Gps, opt => opt.MapFrom(src => src.Gps))
                .ForMember(dest => dest.Billing, opt => opt.MapFrom(src => src.Billing))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.StatusDate, opt => opt.MapFrom(src => src.StatusDate))
                .ForMember(dest => dest.SiteEmail, opt => opt.MapFrom(src => src.SiteEmail))
                .ForMember(dest => dest.LandLine, opt => opt.MapFrom(src => src.LandLine))
                .ForMember(dest => dest.DuressEmail, opt => opt.MapFrom(src => src.DuressEmail))
                .ForMember(dest => dest.DuressSms, opt => opt.MapFrom(src => src.DuressSms))
                .ForMember(dest => dest.UploadGuardLog, opt => opt.MapFrom(src => src.UploadGuardLog))
                .ForMember(dest => dest.UploadFusionLog, opt => opt.MapFrom(src => src.UploadFusionLog))
                .ForMember(dest => dest.GuardLogEmailTo, opt => opt.MapFrom(src => src.GuardLogEmailTo))
                .ForMember(dest => dest.DataCollectionEnabled, opt => opt.MapFrom(src => src.DataCollectionEnabled))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.IsDosDontList, opt => opt.MapFrom(src => src.IsDosDontList))
                .ForMember(dest => dest.MobAppShowClientTypeandSite, opt => opt.MapFrom(src => src.MobAppShowClientTypeandSite));

            CreateMap<ClientSitesLocal, DropdownItemWithAddress>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address));

            CreateMap<FeedbackTemplateViewModel,IrFeedbackTemplateViewModelLocal>()
                .ForMember(dest => dest.TemplateId, opt => opt.MapFrom(src => src.TemplateId))
                .ForMember(dest => dest.TemplateName, opt => opt.MapFrom(src => src.TemplateName))
                .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                .ForMember(dest => dest.FeedbackTypeName, opt => opt.MapFrom(src => src.FeedbackTypeName))
                .ForMember(dest => dest.BackgroundColour, opt => opt.MapFrom(src => src.BackgroundColour))            
                .ForMember(dest => dest.TextColor, opt => opt.MapFrom(src => src.TextColor))
                .ForMember(dest => dest.DeleteStatus, opt => opt.MapFrom(src => src.DeleteStatus))
                .ForMember(dest => dest.SendtoRC, opt => opt.MapFrom(src => src.SendtoRC));

            CreateMap<IrFeedbackTemplateViewModelLocal, FeedbackTemplateViewModel>()
                .ForMember(dest => dest.TemplateId, opt => opt.MapFrom(src => src.TemplateId))
                .ForMember(dest => dest.TemplateName, opt => opt.MapFrom(src => src.TemplateName))
                .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                .ForMember(dest => dest.FeedbackTypeName, opt => opt.MapFrom(src => src.FeedbackTypeName))
                .ForMember(dest => dest.BackgroundColour, opt => opt.MapFrom(src => src.BackgroundColour))
                .ForMember(dest => dest.TextColor, opt => opt.MapFrom(src => src.TextColor))
                .ForMember(dest => dest.DeleteStatus, opt => opt.MapFrom(src => src.DeleteStatus))
                .ForMember(dest => dest.SendtoRC, opt => opt.MapFrom(src => src.SendtoRC));

            CreateMap<AreaItem, ClientSiteAreaLocal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ClientSiteId, opt => opt.MapFrom(src => Convert.ToInt32(src.Value)))
                .ForMember(dest => dest.AreaDetails, opt => opt.MapFrom(src => src.Text));

            CreateMap<ClientSiteAreaLocal, AreaItem>()
                .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.AreaDetails))
                .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.AreaDetails))
                .ForMember(dest => dest.Selected, opt => opt.Ignore());

            CreateMap<Audio.Mp3File, AudioAndMultimediaLocal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AudioType, opt => opt.MapFrom(src => 1))
                .ForMember(dest => dest.LocalFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Label))
                .ForMember(dest => dest.ServerUrl, opt => opt.MapFrom(src => src.Url));

            CreateMap<AudioAndMultimediaLocal, Audio.Mp3File>()
                .ForMember(dest => dest.IsChecked, opt => opt.Ignore())
                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Label))
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.LocalFilePath));

            CreateMap<VideoFile, AudioAndMultimediaLocal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AudioType, opt => opt.MapFrom(src => 3))
                .ForMember(dest => dest.LocalFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Label))
                .ForMember(dest => dest.ServerUrl, opt => opt.MapFrom(src => src.Url));

            CreateMap<AudioAndMultimediaLocal, VideoFile>()
                .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Label))
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.LocalFilePath));
        }
    }
}
