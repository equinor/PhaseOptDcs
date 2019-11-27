##################
Developer's Manual
##################

Project Structure
=================

.. uml::

    @startuml
    scale 1

    class ConfigModel {
        +StreamList Streams
        +ConfigModel ReadConfig(string)
    }

    class Stream {
        +CompositionList Composition
        +Cricondenbar Cricondenbar
        +string Name
    }

    class Component {
        +int Id
        +string Name
        +double ScaleFactor
        +string Tag
        +double Value
        +double GetScaledValue()
    }

    class Cricondenbar {
        +string PressureTag
        +string TemperatureTag
    }

    class CompositionList {
        +List<Component> Item
    }

    class StreamList {
        +List<Stream> Item
    }

    @enduml

