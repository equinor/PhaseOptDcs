.. highlight:: none

#############
User's manual
#############

Introduction
------------

Getting PhaseOptDcs
-------------------

The latest release version of PhaseOptDcs can be downloaded from:
https://github.com/equinor/PhaseOptDcs/releases


Installation
------------

Copy the PhaseOptDcs folder to `C:\\Program Files`.
Open cmd or Powershell and run the installation command::

    PS C:\Program Files\PhaseOptDcs> .\PhaseOptDcs.exe install

This will install PhaseOptDcs as a service that should start automatically when the computer starts.

It is also possible to run PhaseOptDcs directly from the command line::

    PS C:\Program Files\PhaseOptDcs> .\PhaseOptDcs.exe

Running from the command line could be useful for testing.

To uninstall the Windows service run the PhaseOptDcs.exe with the uninstall command::

    PS C:\Program Files\PhaseOptDcs> .\PhaseOptDcs.exe uninstall

This should be done before installing a new version of PhaseOptDcs.

There are several more options available to PhaseOptDcs.exe.
They can be seen by running::

    PS C:\Program Files\PhaseOptDcs> .\PhaseOptDcs.exe --help

This will produce this output:

::

    Command-Line Reference

    PhaseOptDcs.exe [verb] [-option:value] [-switch]

        run                 Runs the service from the command line (default)
        help, --help        Displays help

        install             Installs the service

        --autostart       The service should start automatically (default)
        --disabled        The service should be set to disabled
        --manual          The service should be started manually
        --delayed         The service should start automatically (delayed)
        -instance         An instance name if registering the service
                            multiple times
        -username         The username to run the service
        -password         The password for the specified username
        --localsystem     Run the service with the local system account
        --localservice    Run the service with the local service account
        --networkservice  Run the service with the network service permission
        --interactive     The service will prompt the user at installation for
                            the service credentials
        start             Start the service after it has been installed
        --sudo            Prompts for UAC if running on Vista/W7/2008

        -servicename      The name that the service should use when
                            installing
        -description      The service description the service should use when
                            installing
        -displayname      The display name the the service should use when
                            installing

        start               Starts the service if it is not already running

        stop                Stops the service if it is running

        uninstall           Uninstalls the service

        -instance         An instance name if registering the service
                            multiple times
        --sudo            Prompts for UAC if running on Vista/W7/2008

    Examples:

        PhaseOptDcs.exe install
            Installs the service into the service control manager

        PhaseOptDcs.exe install -username:joe -password:bob --autostart
            Installs the service using the specified username/password and
            configures the service to start automatically at machine startup

        PhaseOptDcs.exe uninstall
            Uninstalls the service

        PhaseOptDcs.exe install -instance:001
            Installs the service, appending the instance name to the service name
            so that the service can be installed multiple times. You may need to
            tweak the log4net.config to make this play nicely with the log files.


Configuration
-------------

The configuration file is structured like the example below.

.. code-block:: xml

    <?xml version="1.0" encoding="utf-8"?>
    <configuration xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
      <OpcUrl>opc.tcp://localhost:62548/Quickstarts/DataAccessServer</OpcUrl>
      <OpcUser>user</OpcUser>
      <OpcPassword>password</OpcPassword>
      <Streams>
        <Stream Name="Stream 1">
        ...
        </Stream>
        <Stream Name="Stream 2">
        ...
        </Stream>
      </Streams>
    </configuration>

-   `<configuration>` is the root element.
    All other elements live inside this one.

-   `<OpcUrl>` is used to select what OPC server to connect to.

-   `<OpcUser>` and `<OpcPassword>` are used to select what user name and password to use to connect to the OPC server.

-   `<Streams>` can contain one or more `<Stream>` elements.

Every `<Stream>` element is structured like below.

.. code-block:: xml

    <Stream Name="Statpipe">
      <Composition>
        <Component Name="CO2"  Id="1"   Tag="ns=2;s=1:AI1001?K" ScaleFactor="1.0" />
        <Component Name="N2"   Id="2"   Tag="ns=2;s=1:AI1001?J" ScaleFactor="1.0" />
        <Component Name="CH4"  Id="101" Tag="ns=2;s=1:AI1001?A" ScaleFactor="1.0" />
        <Component Name="C2H6" Id="201" Tag="ns=2;s=1:AI1001?B" ScaleFactor="1.0" />
        <Component Name="C3"   Id="301" Tag="ns=2;s=1:AI1001?C" ScaleFactor="1.0" />
        <Component Name="iC4"  Id="401" Tag="ns=2;s=1:AI1001?D" ScaleFactor="1.0" />
        <Component Name="nC4"  Id="402" Tag="ns=2;s=1:AI1001?E" ScaleFactor="1.0" />
        <Component Name="iC5"  Id="503" Tag="ns=2;s=1:AI1001?F" ScaleFactor="1.0" />
        <Component Name="nC5"  Id="504" Tag="ns=2;s=1:AI1001?G" ScaleFactor="1.0" />
        <Component Name="2-M-C5"          Id="603"     Tag="ns=2;s=1:AI1001?08" ScaleFactor="0.0001"/>
        <Component Name="3-M-C5"          Id="604"     Tag="ns=2;s=1:AI1001?34" ScaleFactor="0.0001"/>
        <Component Name="nC6"             Id="605"     Tag="ns=2;s=1:AI1001?22" ScaleFactor="0.0001"/>
        <Component Name="C7P / nC7"       Id="701"     Tag="ns=2;s=1:AI1001?46" ScaleFactor="0.0001"/>
        <Component Name="C7N / cy-C6"     Id="606"     Tag="ns=2;s=1:AI1001?47" ScaleFactor="0.0001"/>
        <Component Name="C7A / Benzene"   Id="608"     Tag="ns=2;s=1:AI1001?48" ScaleFactor="0.0001"/>
        <Component Name="C8P / nC8"       Id="801"     Tag="ns=2;s=1:AI1001?49" ScaleFactor="0.0001"/>
        <Component Name="C8N / cy-C7"     Id="707"     Tag="ns=2;s=1:AI1001?50" ScaleFactor="0.0001"/>
        <Component Name="C8A / Toluene"   Id="710"     Tag="ns=2;s=1:AI1001?51" ScaleFactor="0.0001"/>
        <Component Name="C9P / nC9"       Id="901"     Tag="ns=2;s=1:AI1001?52" ScaleFactor="0.0001"/>
        <Component Name="C9N / cy-C8"     Id="806"     Tag="ns=2;s=1:AI1001?53" ScaleFactor="0.0001"/>
        <Component Name="C9A / m-xylene"  Id="809"     Tag="ns=2;s=1:AI1001?54" ScaleFactor="0.0001"/>
        <Component Name="nC10"            Id="1016"    Tag="ns=2;s=1:AI1001?05" ScaleFactor="0.0001"/>
      </Composition>
      <Cricondenbar>
        <Pressure    Name="Pressure Name"     Tag="ns=2;s=1:AI1001?Pressure"    Type="single" />
        <Temperature Name="Temperature Name"  Tag="ns=2;s=1:AI1001?Temperature" Type="single" />
      </Cricondenbar>
      <LiquidDropouts>
        <LiquidDropout>
          <WorkingPoint Name="Kårstø">
            <Pressure    Name="Pressure Name"    Tag="ns=2;s=1:PI1001?Measurement" Unit="barg" />
            <Temperature Name="Temperature Name" Tag="ns=2;s=1:TI1001?Measurement" Unit="C" />
            <Margin      Name="Margin Name"      Tag="ns=2;s=1:PI1002?Measurement" Unit="barg" Type="single" />
            <DewPoint    Name="DewPoint Name"    Tag="ns=2;s=1:PI1003?Measurement" Unit="barg" Type="single" />
          </WorkingPoint>
        </LiquidDropout>
      </LiquidDropouts>
    </Stream>

A `<Stream>` element has the following attributes:

-   `Name` is used to identify the stream.
    This text will appear in the log file to identyfy the stream.


A Stream must have one Composition element.

A `<Composition>` contains one or more `<Component>` elements.
A `<Component>`has the flollowing attributes:

-   `Name` is give the component a human readable name.

-   `Id` is used to identify the component to the UMR calculation.

-   `Tag` is the OPC item for the component.
    The value of this item is read from the OPC server.

-   `ScaleFactor` is used to scale the value into the proper range for the UMR calculation.

A `<Stream>` can have one `<Cricondenbar>` element.
By having one `<Cricondenbar>` element, PhaseOptDcs will calculate the cricondenbar point of the composition.
The cricondenbar point consists of a pressure value and a temperature value.
The `<Cricondenbar>` element can contain a `<Pressure>` and a `<Temperature>` element.
Or it can contian just one of them.
The `<Pressure>` and `<Temperature>` elements have the following attributes:

-   `Name`
-   `Tag` is the OPC item where the value will be written to.
-   `Type` is the datatype

Files
-----
