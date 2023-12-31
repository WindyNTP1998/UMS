using System.Linq.Expressions;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.JsonSerialization;
using UMS.Platform.Common.Validations;
using UMS.Platform.Common.Validations.Validators;

namespace UMS.Platform.Domain.Entities;

/// <summary>
///     This interface is used for conventional type scan for entity
/// </summary>
public interface IEntity
{
}

public interface IEntity<TPrimaryKey> : IEntity
{
    public TPrimaryKey Id { get; set; }
}

public interface IValidatableEntity
{
    public PlatformValidationResult Validate();
}

public interface IValidatableEntity<TEntity> : IValidatableEntity
{
    public new PlatformValidationResult<TEntity> Validate();
}

public interface ISupportDomainEventsEntity
{
    /// <summary>
    ///     DomainEvents is used to give more detail about the domain event action inside entity.<br />
    ///     It is a list of DomainEventTypeName-DomainEventAsJson from entity domain events
    /// </summary>
    public List<KeyValuePair<string, DomainEvent>> GetDomainEvents();

    public ISupportDomainEventsEntity AddDomainEvent<TEvent>(TEvent domainEvent, string customDomainEventName = null)
        where TEvent : DomainEvent;

    public abstract class DomainEvent
    {
        public static string GetDefaultEventName<TEvent>() where TEvent : DomainEvent
        {
            return typeof(TEvent).Name;
        }
    }

    public class FieldUpdatedDomainEvent : DomainEvent
    {
        public string FieldName { get; set; }
        public object OriginalValue { get; set; }
        public object NewValue { get; set; }

        public static FieldUpdatedDomainEvent Create(string propertyName, object originalValue, object newValue)
        {
            return new FieldUpdatedDomainEvent
            {
                FieldName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }

    public class FieldUpdatedDomainEvent<TValue> : FieldUpdatedDomainEvent
    {
        public new TValue OriginalValue { get; set; }
        public new TValue NewValue { get; set; }

        public static FieldUpdatedDomainEvent<TValue> Create(string propertyName, TValue originalValue, TValue newValue)
        {
            return new FieldUpdatedDomainEvent<TValue>
            {
                FieldName = propertyName,
                OriginalValue = originalValue,
                NewValue = newValue
            };
        }
    }
}

public interface ISupportDomainEventsEntity<out TEntity> : ISupportDomainEventsEntity
    where TEntity : class, IEntity, new()
{
    public new TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null)
        where TEvent : DomainEvent;
}

public interface IValidatableEntity<TEntity, TPrimaryKey> : IValidatableEntity<TEntity>, IEntity<TPrimaryKey>
    where TEntity : IEntity<TPrimaryKey>
{
    /// <summary>
    ///     Default return null. Default check unique is by Id. <br />
    ///     If return not null, this will be used instead to check the entity is unique to create or update
    /// </summary>
    public PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator();
}

public abstract class Entity<TEntity, TPrimaryKey>
    : IValidatableEntity<TEntity, TPrimaryKey>, ISupportDomainEventsEntity<TEntity>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    protected readonly List<KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>> DomainEvents = new();

    public List<KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>> GetDomainEvents()
    {
        return DomainEvents;
    }

    ISupportDomainEventsEntity ISupportDomainEventsEntity.AddDomainEvent<TEvent>(TEvent domainEvent,
        string customDomainEventName)
    {
        return AddDomainEvent(domainEvent, customDomainEventName);
    }

    public TEntity AddDomainEvent<TEvent>(TEvent eventActionPayload, string customDomainEventName = null)
        where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        DomainEvents.Add(new KeyValuePair<string, ISupportDomainEventsEntity.DomainEvent>(
            customDomainEventName ?? ISupportDomainEventsEntity.DomainEvent.GetDefaultEventName<TEvent>(),
            eventActionPayload));
        return (TEntity)this;
    }

    public virtual TPrimaryKey Id { get; set; }

    /// <summary>
    ///     Help to validate entity create/update must be unique. <br />
    ///     Example: <br />
    ///     public override PlatformCheckUniqueValidator[EmployeeRemainingAttendance] CheckUniqueValidator()
    ///     {
    ///     return new PlatformCheckUniqueValidator[EmployeeRemainingAttendance](
    ///     targetItem: this,
    ///     findOtherDuplicatedItemExpr: otherItem =>
    ///     !otherItem.Id.Equals(Id) && otherItem.EmployeeId == EmployeeId && otherItem.AttendanceTypeId == AttendanceTypeId &&
    ///     otherItem.CompanyId == CompanyId,
    ///     "EmployeeRemainingAttendance must be unique");
    ///     }
    /// </summary>
    public virtual PlatformCheckUniqueValidator<TEntity> CheckUniqueValidator()
    {
        return null;
    }

    public virtual PlatformValidationResult<TEntity> Validate()
    {
        var validator = GetValidator();

        return validator != null ? validator.Validate((TEntity)this) : PlatformValidationResult.Valid((TEntity)this);
    }

    PlatformValidationResult IValidatableEntity.Validate()
    {
        return Validate();
    }

    public TEntity AddFieldUpdatedEvent<TValue>(string propertyName, TValue originalValue, TValue newValue)
    {
        return this.As<TEntity>().AddFieldUpdatedEvent<TEntity, TValue>(propertyName, originalValue, newValue);
    }

    public TEntity AddFieldUpdatedEvent<TValue>(Expression<Func<TEntity, TValue>> property, TValue originalValue,
        TValue newValue)
    {
        return this.As<TEntity>().AddFieldUpdatedEvent<TEntity, TValue>(property, originalValue, newValue);
    }

    public List<TEvent> FindDomainEvents<TEvent>()
        where TEvent : ISupportDomainEventsEntity.DomainEvent
    {
        return this.As<TEntity>().FindDomainEvents<TEntity, TEvent>();
    }

    public List<ISupportDomainEventsEntity.FieldUpdatedDomainEvent<TValue>> FindFieldUpdatedDomainEvents<TValue>(
        string propertyName)
    {
        return this.As<TEntity>().FindFieldUpdatedDomainEvents<TEntity, TValue>(propertyName);
    }

    public virtual TEntity Clone()
    {
        // doNotTryUseRuntimeType = true to Serialize normally not using the runtime type to prevent error.
        // If using runtime type, the ef core entity lazy loading proxies will be the runtime type => lead to error
        return PlatformJsonSerializer.Deserialize<TEntity>(PlatformJsonSerializer.Serialize(this.As<TEntity>()));
    }

    /// <summary>
    ///     To get the entity validator. <br />
    ///     This will help us to centralize and reuse domain validation logic. Ensure any request which update/create domain
    ///     entity
    ///     use the same entity validation logic (Single Responsibility, Don't Repeat YourSelf).
    /// </summary>
    public virtual PlatformValidator<TEntity> GetValidator()
    {
        return null;
    }
}

public interface IRootEntity<TPrimaryKey> : IEntity<TPrimaryKey>
{
}

/// <summary>
///     Root entity represent an aggregate root entity. Only root entity can be Create/Update/Delete via repository
/// </summary>
public abstract class RootEntity<TEntity, TPrimaryKey> : Entity<TEntity, TPrimaryKey>, IRootEntity<TPrimaryKey>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
}