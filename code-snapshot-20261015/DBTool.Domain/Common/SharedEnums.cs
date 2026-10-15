namespace DBTool.Domain;
public enum CalculationScope { BaseRebarOnly, FullRebar, ConcreteOnly, FormworkOnly, Full, GeometryOnly }
public enum ElevationMode { Absolute, Symbolic }
public enum EvidenceKind { T, X, C, S, U }
public enum EvidenceStatus { Documented, ObservedReference, LegacyObserved, Derived, Unresolved, ExperimentalSoftwareFixture }
public enum Profile { GaThamLienKhoi, GaThamNapRoi, GaThamThuNapRoi }
public enum WallId { T1, T2, T3, T4 }
public enum ComponentKind { Pad, Base, Wall, Roof, Neck, Collar, Panel, CatchBasin }
public enum Layer { Bottom, Top }
public enum Axis { X, Y }
public enum CalculationStatus { NotCalculated, Calculated }
