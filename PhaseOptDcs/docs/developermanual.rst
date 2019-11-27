##################
Developer's Manual
##################

Project Structure
=================

.. uml::

    @startuml
    scale 1

    class ConfigModel {
        +Streams
        +ReadConfig(String)
    }

    class Stream {
        +Composition
        +Cricondenbar
        +Name
    }

    class Component {
        +Id
        +Name
        +ScaleFactor
        +Tag
        +Value
        +GetScaledValue()
    }

    class Cricondenbar {
        +PressureTag
        +TemperatureTag
    }

    class CompositionList {
        +Item
    }

    class StreamList {
        +Item
    }

    @enduml

