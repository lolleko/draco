// Copyright 2024 The Draco Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Draco.Decoder;

public enum StatusCode
{
    Ok = 0,
    DracoError = 1,
    IoError = 2,
    InvalidParameter = 3,
    UnsupportedVersion = 4,
    UnknownVersion = 5
}

public readonly struct Status
{
    public StatusCode Code { get; }
    public string ErrorMessage { get; }

    private Status(StatusCode code, string errorMessage)
    {
        Code = code;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public bool Ok => Code == StatusCode.Ok;

    public static Status OkStatus() => new(StatusCode.Ok, string.Empty);
    public static Status Error(StatusCode code, string message) => new(code, message);
    public static Status DracoError(string message) => new(StatusCode.DracoError, message);
    public static Status IoError(string message) => new(StatusCode.IoError, message);
    public static Status InvalidParameter(string message) => new(StatusCode.InvalidParameter, message);
    public static Status UnsupportedVersion(string message) => new(StatusCode.UnsupportedVersion, message);

    public override string ToString() => Ok ? "OK" : $"{Code}: {ErrorMessage}";
}

public readonly struct StatusOr<T>
{
    private readonly T value;
    public Status Status { get; }

    private StatusOr(Status status, T value = default)
    {
        Status = status;
        this.value = value;
    }

    public bool Ok => Status.Ok;
    public T Value => Ok ? value : throw new InvalidOperationException($"Cannot get value from error status: {Status}");

    public static StatusOr<T> FromValue(T value) => new(Status.OkStatus(), value);
    public static StatusOr<T> FromStatus(Status status) => new(status, default);

    public static implicit operator StatusOr<T>(T value) => FromValue(value);
    public static implicit operator StatusOr<T>(Status status) => FromStatus(status);
}
