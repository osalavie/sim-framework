using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Mapping;
using Sim.System.Application.Contracts.Dtos;
using Sim.System.Application.Contracts.Permission;
using Sim.System.Application.Contracts.Role;
using Sim.System.Application.Contracts.RouteConfig;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Role;

namespace Sim.Security.Tests;

public class RbacAuditDtoTests
{
    public static TheoryData<Type> ManagementDtoTypes => new()
    {
        typeof(UserDto),
        typeof(RoleDto),
        typeof(PermissionDto),
        typeof(ModuleConfigDto),
        typeof(RouteConfigDto)
    };

    [Theory]
    [MemberData(nameof(ManagementDtoTypes))]
    public void ManagementDtosExposeCompleteAuditFields(Type dtoType)
    {
        Assert.True(
            typeof(AuditedEntityDto<Guid>).IsAssignableFrom(dtoType),
            $"{dtoType.Name} must expose creation and modification audit fields.");
        Assert.NotNull(dtoType.GetProperty(nameof(CreationAuditedEntityDto<Guid>.CreationTime)));
        Assert.NotNull(dtoType.GetProperty(nameof(CreationAuditedEntityDto<Guid>.CreatorId)));
        Assert.NotNull(dtoType.GetProperty(nameof(AuditedEntityDto<Guid>.LastModificationTime)));
        Assert.NotNull(dtoType.GetProperty(nameof(AuditedEntityDto<Guid>.LastModifierId)));
    }

    [Fact]
    public void ObjectMapperCopiesAuditValuesToManagementDtos()
    {
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var creationTime = new DateTime(2026, 7, 29, 8, 30, 0, DateTimeKind.Utc);
        var modificationTime = creationTime.AddHours(1);
        var entity = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Audited role",
            Code = "audited-role",
            CreationTime = creationTime,
            CreatorId = creatorId,
            LastModificationTime = modificationTime,
            LastModifierId = modifierId
        };

        var dto = new MapsterObjectMapper().Map<RoleEntity, RoleDto>(entity);

        Assert.Equal(creationTime, dto.CreationTime);
        Assert.Equal(creatorId, dto.CreatorId);
        Assert.Equal(modificationTime, dto.LastModificationTime);
        Assert.Equal(modifierId, dto.LastModifierId);
    }
}
