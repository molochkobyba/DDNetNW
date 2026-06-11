using System.Collections.Generic;

namespace DDNetNW.Models;

public sealed record DdnetDataSnapshot(IReadOnlyList<ServerSnapshot> Servers);
