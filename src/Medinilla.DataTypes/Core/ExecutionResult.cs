using System;
using System.Collections.Generic;
using System.Text;

namespace Medinilla.DataTypes.Core;

public sealed record ExecutionResult(string MessageId, string ActionName, bool Error, string? ErrorMessage);
