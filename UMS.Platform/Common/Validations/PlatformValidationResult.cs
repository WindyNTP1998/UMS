using FluentValidation.Results;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;
using UMS.Platform.Common.Validations.Exceptions;
using UMS.Platform.Common.Validations.Extensions;

namespace UMS.Platform.Common.Validations;

public class PlatformValidationResult<TValue> : ValidationResult
{
    private List<PlatformValidationError> finalCombinedValidationErrors;

    public PlatformValidationResult()
    {
    }

    public PlatformValidationResult(TValue value,
        List<PlatformValidationError> failures,
        Func<PlatformValidationResult<TValue>, Exception> invalidException = null) : base(failures ??
        new List<PlatformValidationError>())
    {
        Value = value;
        RootValidationErrors = failures ?? new List<PlatformValidationError>();
        InvalidException = invalidException;
    }

    public override bool IsValid => !Errors.Any();

    public TValue Value { get; protected set; }
    public Func<PlatformValidationResult<TValue>, Exception> InvalidException { get; set; }

    public new List<PlatformValidationError> Errors =>
        finalCombinedValidationErrors ??= FinalCombinedValidation().RootValidationErrors;

    protected List<PlatformValidationError> RootValidationErrors { get; } = new();
    protected bool IsRootValidationValid => !RootValidationErrors.Any();

    protected List<LogicalAndValidationsChainItem> LogicalAndValidationsChain { get; set; } = new();

    /// <summary>
    ///     Dictionary map from LogicalAndValidationsChainItem Position to ExceptionCreatorFn
    /// </summary>
    protected Dictionary<int, Func<PlatformValidationResult<TValue>, Exception>>
        LogicalAndValidationsChainInvalidExceptions { get; } =
        new();

    private PlatformValidationResult<TValue> FinalCombinedValidation()
    {
        return StandaloneRootValidation()
            .Pipe(selfValidation => IsRootValidationValid ? CombinedLogicalAndValidationsChain() : selfValidation);
    }

    private PlatformValidationResult<TValue> CombinedLogicalAndValidationsChain()
    {
        return LogicalAndValidationsChain
            .Aggregate(Valid(Value),
                (prevVal, nextValChainItem) => prevVal.IsValid ? nextValChainItem.ValidationFn(prevVal.Value) : prevVal,
                valResult => valResult);
    }

    private PlatformValidationResult<TValue> StandaloneRootValidation()
    {
        return RootValidationErrors.Any() ? Invalid(Value, RootValidationErrors.ToArray()) : Valid(Value);
    }

    public static implicit operator TValue(PlatformValidationResult<TValue> validation)
    {
        return validation.EnsureValid();
    }

    public static implicit operator bool(PlatformValidationResult<TValue> validation)
    {
        return validation.IsValid;
    }

    public static implicit operator string(PlatformValidationResult<TValue> validation)
    {
        return validation.ToString();
    }

    public static implicit operator PlatformValidationResult<TValue>((TValue value, string error) invalidValidationInfo)
    {
        return Invalid(invalidValidationInfo.value, invalidValidationInfo.error);
    }

    public static implicit operator PlatformValidationResult<TValue>(
        (TValue value, List<string> errors) invalidValidationInfo)
    {
        return invalidValidationInfo.errors?.Any() == true
            ? Invalid(invalidValidationInfo.value,
                invalidValidationInfo.errors.Select(p => (PlatformValidationError)p).ToArray())
            : Valid(invalidValidationInfo.value);
    }

    public static implicit operator PlatformValidationResult<TValue>(
        (TValue value, List<PlatformValidationError> errors) invalidValidationInfo)
    {
        return invalidValidationInfo.errors?.Any() == true
            ? Invalid(invalidValidationInfo.value, invalidValidationInfo.errors.Select(p => p).ToArray())
            : Valid(invalidValidationInfo.value);
    }

    public static implicit operator PlatformValidationResult(PlatformValidationResult<TValue> validation)
    {
        return new PlatformValidationResult(validation.Value, validation.Errors);
    }

    /// <summary>
    ///     Return a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    internal static PlatformValidationResult<TValue> Valid(TValue value = default)
    {
        return new PlatformValidationResult<TValue>(value, null);
    }

    /// <summary>
    ///     Return a invalid validation result.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A invalid validation result.</returns>
    internal static PlatformValidationResult<TValue> Invalid(TValue value,
        params PlatformValidationError[] errors)
    {
        return errors?.Any() == true
            ? new PlatformValidationResult<TValue>(value, errors.ToList())
            : new PlatformValidationResult<TValue>(value,
                Util.ListBuilder.New<PlatformValidationError>("Invalid!"));
    }

    /// <summary>
    ///     Return a valid validation result if the condition is true, otherwise return a invalid validation with errors.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="must">The valid condition.</param>
    /// <param name="errors">The errors if the valid condition is false.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> Validate(TValue value,
        bool must,
        params PlatformValidationError[] errors)
    {
        return Validate(value, () => must, errors);
    }

    /// <inheritdoc cref="Validate(TValue,bool,PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> Validate(TValue value,
        Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return must() ? Valid(value) : Invalid(value, errors);
    }

    /// <summary>
    ///     Return a invalid validation result with errors if the condition is true, otherwise return a valid.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="mustNot">The invalid condition.</param>
    /// <param name="errors">The errors if the invalid condition is true.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> ValidateNot(TValue value,
        bool mustNot,
        params PlatformValidationError[] errors)
    {
        return ValidateNot(value, () => mustNot, errors);
    }

    /// <inheritdoc cref="ValidateNot(TValue,bool,PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> ValidateNot(TValue value,
        Func<bool> mustNot,
        params PlatformValidationError[] errors)
    {
        return mustNot() ? Invalid(value, errors) : Valid(value);
    }

    /// <summary>
    ///     Combine all validation but fail fast. Ex: [Val1, Val2].Combine() = Val1 && Val2
    /// </summary>
    public static PlatformValidationResult<TValue> Combine(params Func<PlatformValidationResult<TValue>>[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate((prevVal, nextVal) => () => prevVal().ThenValidate(p => nextVal()))();
    }

    /// <inheritdoc cref="Combine(System.Func{PlatformValidationResult{TValue}}[])" />
    public static PlatformValidationResult<TValue> Combine(params PlatformValidationResult<TValue>[] validations)
    {
        return Combine(validations.Select(p => (Func<PlatformValidationResult<TValue>>)(() => p)).ToArray());
    }

    /// <summary>
    ///     Aggregate all validations, collect all validations errors. Ex: [Val1, Val2].Combine() = Val1 & Val2 (Mean that
    ///     execute both Val1 and Val2, then harvest return all errors from both all validations in list)
    /// </summary>
    public static PlatformValidationResult<TValue> Aggregate(params PlatformValidationResult<TValue>[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate((prevVal, nextVal) =>
                new PlatformValidationResult<TValue>(nextVal.Value, prevVal.Errors.Concat(nextVal.Errors).ToList()));
    }

    /// <inheritdoc cref="Aggregate(PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult<TValue> Aggregate(TValue value,
        params (bool, PlatformValidationError)[] validations)
    {
        return Aggregate(validations
            .Select(validationInfo => Validate(value, validationInfo.Item1, validationInfo.Item2))
            .ToArray());
    }

    /// <inheritdoc cref="Aggregate(PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult<TValue> Aggregate(
        params Func<PlatformValidationResult<TValue>>[] validations)
    {
        return Aggregate(validations.Select(p => p()).ToArray());
    }

    public string ErrorsMsg()
    {
        return Errors?.Aggregate(string.Empty,
            (currentMsg, error) => $"{(currentMsg == string.Empty ? string.Empty : $"{currentMsg}; ")}{error}");
    }

    public override string ToString()
    {
        return ErrorsMsg();
    }

    public PlatformValidationResult<T> Then<T>(Func<TValue, T> next)
    {
        return Match(value => new PlatformValidationResult<T>(next(value), null),
            err => Of<T>(default));
    }

    /// <summary>
    ///     Performs an additional asynchronous validation operation on the value using the specified nextVal function if this
    ///     validation is valid.
    /// </summary>
    public Task<PlatformValidationResult<T>> ThenAsync<T>(Func<Task<PlatformValidationResult<T>>> nextVal)
    {
        return MatchAsync(value => nextVal(),
            err => Of<T>(default).ToTask());
    }

    /// <inheritdoc cref="ThenAsync{T}(Func{Task{PlatformValidationResult{T}}})" />
    public Task<PlatformValidationResult<T>> ThenAsync<T>(Func<TValue, Task<PlatformValidationResult<T>>> nextVal)
    {
        return MatchAsync(value => nextVal(Value),
            err => Of<T>(default).ToTask());
    }

    /// <inheritdoc cref="ThenAsync{T}(Func{Task{PlatformValidationResult{T}}})" />
    public async Task<PlatformValidationResult<T>> ThenAsync<T>(Func<TValue, Task<T>> next)
    {
        return await MatchAsync(async value => new PlatformValidationResult<T>(await next(value), null),
            err => Of<T>(default).ToTask());
    }

    /// <summary>
    ///     Executes a specified function based on whether the validation result is valid or invalid.
    /// </summary>
    public PlatformValidationResult<T> Match<T>(Func<TValue, PlatformValidationResult<T>> valid,
        Func<IEnumerable<PlatformValidationError>, PlatformValidationResult<T>> invalid)
    {
        return IsValid ? valid(Value) : invalid(Errors);
    }

    /// <summary>
    ///     Executes a specified asynchronous function based on whether the validation result is valid or invalid.
    /// </summary>
    public async Task<PlatformValidationResult<T>> MatchAsync<T>(Func<TValue, Task<PlatformValidationResult<T>>> valid,
        Func<IEnumerable<PlatformValidationError>, Task<PlatformValidationResult<T>>> invalid)
    {
        return IsValid ? await valid(Value) : await invalid(Errors);
    }

    public PlatformValidationResult<TValue> And(Func<TValue, PlatformValidationResult<TValue>> nextValidation)
    {
        LogicalAndValidationsChain.Add(new LogicalAndValidationsChainItem
        {
            ValidationFn = nextValidation,
            Position = LogicalAndValidationsChain.Count
        });
        return this;
    }

    public PlatformValidationResult<TValue> And(Func<TValue, bool> must,
        params PlatformValidationError[] errors)
    {
        return And(() => Validate(Value, () => must(Value), errors));
    }

    public async Task<PlatformValidationResult<TValue>> AndAsync(Func<TValue, Task<bool>> must,
        params PlatformValidationError[] errors)
    {
        return await AndAsync(_ => _.ValidateAsync(must, errors));
    }

    public PlatformValidationResult<TValue> And(Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return And(() => Validate(Value, must, errors));
    }

    public PlatformValidationResult<TValue> And(Func<PlatformValidationResult<TValue>> nextValidation)
    {
        return And(value => nextValidation());
    }

    /// <summary>
    ///     Validation[T] => and Validation[T1] => Validation[T1]
    /// </summary>
    public PlatformValidationResult<TNextValidation> ThenValidate<TNextValidation>(
        Func<TValue, PlatformValidationResult<TNextValidation>> nextValidation)
    {
        return IsValid
            ? nextValidation(Value)
            : PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray());
    }

    public PlatformValidationResult<TValue> And<TNextValidation>(
        Func<TValue, PlatformValidationResult<TNextValidation>> nextValidation)
    {
        return IsValid ? nextValidation(Value).Of(Value) : Invalid(default, Errors.ToArray());
    }

    public async Task<PlatformValidationResult<TValue>> And(Task<PlatformValidationResult<TValue>> nextValidation)
    {
        return !IsValid ? this : await nextValidation;
    }

    public async Task<PlatformValidationResult<TValue>> AndAsync(
        Func<TValue, Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return !IsValid ? this : await nextValidation(Value);
    }

    /// <summary>
    ///     Validation[T] => and => Validation[T1]
    /// </summary>
    public async Task<PlatformValidationResult<TNextValidation>> ThenValidateAsync<TNextValidation>(
        Func<TValue, Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid
            ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray())
            : await nextValidation(Value);
    }

    public async Task<PlatformValidationResult<TValue>> AndAsync<TNextValidation>(
        Func<TValue, Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid
            ? Invalid(default, Errors.ToArray())
            : await nextValidation(Value).Then(nextValResult => nextValResult.Of(Value));
    }

    public PlatformValidationResult<TValue> AndNot(Func<TValue, bool> mustNot,
        params PlatformValidationError[] errors)
    {
        return And(() => ValidateNot(Value, () => mustNot(Value), errors));
    }

    public async Task<PlatformValidationResult<TValue>> AndNotAsync(Func<TValue, Task<bool>> mustNot,
        params PlatformValidationError[] errors)
    {
        return await AndAsync(_ => _.ValidateNotAsync(mustNot, errors));
    }

    public PlatformValidationResult<TValue> Or(Func<PlatformValidationResult<TValue>> nextValidation)
    {
        return IsValid ? this : nextValidation();
    }

    public async Task<PlatformValidationResult<TValue>> Or(Task<PlatformValidationResult<TValue>> nextValidation)
    {
        return IsValid ? this : await nextValidation;
    }

    public async Task<PlatformValidationResult<TValue>> Or(Func<Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return IsValid ? this : await nextValidation();
    }

    /// <summary>
    ///     Throws an exception if the validation result is invalid. It returns the validated value if the result is valid.
    /// </summary>
    public TValue EnsureValid(Func<PlatformValidationResult<TValue>, Exception> invalidException = null)
    {
        if (!IsValid)
            throw invalidException != null
                ? invalidException(this)
                : InvalidException != null
                    ? InvalidException(this)
                    : new PlatformValidationException(this);

        return Value;
    }

    public PlatformValidationResult<T> Of<T>(T value)
    {
        return new PlatformValidationResult<T>(value,
            Errors,
            InvalidException != null ? val => InvalidException(this) : null);
    }

    public PlatformValidationResult<T> Of<T>()
    {
        return new PlatformValidationResult<T>(Value.Cast<T>(),
            Errors,
            InvalidException != null ? val => InvalidException(this) : null);
    }

    /// <summary>
    ///     Use this to set the specific exception for the current validation chain. So that when use ensure valid, each
    ///     validation condition chain could throw the attached exception
    /// </summary>
    public PlatformValidationResult<TValue> WithInvalidException(
        Func<PlatformValidationResult<TValue>, Exception> invalidException)
    {
        if (!LogicalAndValidationsChain.Any())
            InvalidException = invalidException;
        else
            LogicalAndValidationsChain
                .Where(andValidationsChainItem =>
                    !LogicalAndValidationsChainInvalidExceptions.ContainsKey(andValidationsChainItem.Position))
                .Ensure(notSetInvalidExceptionAndValidations => notSetInvalidExceptionAndValidations.Any(),
                    "All InvalidException has been set")
                .ForEach(notSetInvalidExceptionAndValidation =>
                    LogicalAndValidationsChainInvalidExceptions.Add(notSetInvalidExceptionAndValidation.Position,
                        invalidException));

        return this;
    }

    /// <summary>
    ///     Do validation all conditions in AndConditions Chain when .And().And() and collect all errors
    ///     <br />
    ///     "andValidationChainItem => Util.TaskRunner.CatchException(" Explain:
    ///     Because the and chain could depend on previous chain, so do harvest validation errors could throw errors on some
    ///     depended validation, so we catch and ignore the depended chain item
    ///     try to get all available interdependent/not depend on prev validation item chain validations
    /// </summary>
    public List<PlatformValidationError> AggregateErrors()
    {
        return Util.ListBuilder.New(StandaloneRootValidation().Errors.ToArray())
            .Concat(LogicalAndValidationsChain.SelectMany(andValidationChainItem => Util.TaskRunner.CatchException(
                () => andValidationChainItem.ValidationFn(Value).Errors,
                new List<PlatformValidationError>())))
            .ToList();
    }

    public class LogicalAndValidationsChainItem
    {
        public Func<TValue, PlatformValidationResult<TValue>> ValidationFn { get; set; }
        public int Position { get; set; }
    }
}

public class PlatformValidationResult : PlatformValidationResult<object>
{
    public PlatformValidationResult(object value,
        List<PlatformValidationError> failures,
        Func<PlatformValidationResult<object>, Exception> invalidException = null) : base(value, failures,
        invalidException)
    {
    }

    public static implicit operator PlatformValidationResult(string error)
    {
        return Invalid<object>(null, error);
    }

    public static implicit operator PlatformValidationResult(List<string> errors)
    {
        return Invalid<object>(null, errors?.Select(p => (PlatformValidationError)p).ToArray());
    }

    public static implicit operator PlatformValidationResult(List<PlatformValidationError> errors)
    {
        return Invalid<object>(null, errors.ToArray());
    }

    /// <summary>
    ///     Return a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    public static PlatformValidationResult<TValue> Valid<TValue>(TValue value = default)
    {
        return new PlatformValidationResult<TValue>(value, null);
    }

    /// <summary>
    ///     Return a invalid validation result.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A invalid validation result.</returns>
    public static PlatformValidationResult<TValue> Invalid<TValue>(TValue value,
        params PlatformValidationError[] errors)
    {
        return errors?.Any(p => p?.ToString().IsNotNullOrEmpty() == true) == true
            ? new PlatformValidationResult<TValue>(value, errors.ToList())
            : new PlatformValidationResult<TValue>(value,
                Util.ListBuilder.New<PlatformValidationError>("Invalid!"));
    }

    /// <summary>
    ///     Return a valid validation result if the condition is true, otherwise return a invalid validation with errors.
    /// </summary>
    /// <param name="must">The valid condition.</param>
    /// <param name="errors">The errors if the valid condition is false.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> Validate<TValue>(TValue value,
        bool must,
        params PlatformValidationError[] errors)
    {
        return must ? Valid(value) : Invalid(value, errors);
    }

    /// <inheritdoc cref="Validate(bool,PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> Validate<TValue>(TValue value,
        Func<bool> validConditionFunc,
        params PlatformValidationError[] errors)
    {
        return Validate(value, validConditionFunc(), errors);
    }

    /// <inheritdoc cref="PlatformValidationResult{TValue}.Combine(Func{PlatformValidationResult{TValue}}[])" />
    public static PlatformValidationResult Combine(params Func<PlatformValidationResult>[] validations)
    {
        return validations.Aggregate(Valid(validations[0]().Value),
            (acc, validator) => acc.ThenValidate(p => validator()));
    }

    /// <inheritdoc cref="PlatformValidationResult{TValue}.Aggregate(PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult Aggregate(params PlatformValidationResult[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate((prevVal, nextVal) =>
                new PlatformValidationResult(nextVal.Value, prevVal.Errors.Concat(nextVal.Errors).ToList()));
    }

    public PlatformValidationResult And(Func<PlatformValidationResult> nextValidation)
    {
        return IsValid ? nextValidation() : Invalid(Value, Errors.ToArray());
    }

    public async Task<PlatformValidationResult> AndAsync(Func<Task<PlatformValidationResult>> nextValidation)
    {
        return !IsValid ? this : await nextValidation();
    }

    public async Task<PlatformValidationResult<TNextValidation>> AndThenAsync<TNextValidation>(
        Func<Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid
            ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray())
            : await nextValidation();
    }

    public async Task<PlatformValidationResult<object>> AndAsync<TNextValidation>(
        Func<Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid
            ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray())
            : await nextValidation().Then(nextValResult => nextValResult.Of(Value));
    }

    public TValue EnsureValid<TValue>(Func<PlatformValidationResult, Exception> invalidException = null)
    {
        return EnsureValid(invalidException != null ? _ => invalidException(this) : null).Cast<TValue>();
    }
}