This is a Scalarm Pathfinder supervisor. For more information see: https://github.com/Scalarm/scalarm_experiment_supervisor

# Dependencies

This program requires following libraries placed in its root directory to run:
* ``Newtonsoft.Json.dll`` - version >= 6.0.8
* ``RestSharp.dll`` - version >= 105.0.1
* ``Scalarm.dll`` - can be found here: https://github.com/Scalarm/scalarm_client_csharp
* ``SensitivityAnalysis.dll`` - a proprietary sensitivity analysis library by Daniel Bachniak; not available publicly at the moment

# Building

Program builds in .Net4.0 compiler - both Mono and VisualStudio, but only Mono is officially supported and tested. The easiest way is to open project file in IDE, satisfy dependencies (listed above) and build the project.

# Execution

Please put all dependencies DLLs in compiled program root directory and launch Program.exe with Mono/.Net runtime.
**At least Mono 4.2 runtime is required to run.** The program will not run under Mono 3.x!


## Execution parameters

* ``mono Program.exe`` will read config from ``config.json`` in current directory
* ``mono Program.exe -config <config_path>`` will read config from ``<config_path>`` file
* ``mono Program.exe -stdin`` will read config from standard input (e.g. ``cat config.json | Program.exe`` on \*NIX)


# Configuration

## Example configurations

### Method of Morris

```json
{
  "method_type": "morris",
  "morris_samples_count": 2,
  "morris_levels_count": 3,
  "experiment_id": "56b1b4f9f83f4910c0000261",
  "user": "f784b56e-e02c-4ede-9d85-fb3ed5566cc5",
  "password":" <ciach>",
  "parameters": [
    {
      "id":"parameter1",
      "min":0,
      "max":1000
    },
    {
      "id":"parameter2",
      "min":-100,
      "max":100
    }
  ],
  "address": "149.156.11.4:10053"
}
```

### Method of Sobol

```json
{
  "method_type": "sobol",
  "sobol_base_inputs_count": 1000,
  "experiment_id": "56b1b4f9f83f4910c0000261",
  "user": "f784b56e-e02c-4ede-9d85-fb3ed5566cc5",
  "password":" <ciach>",
  "parameters": [
    {
      "id":"parameter1",
      "min":0,
      "max":1000
    },
    {
      "id":"parameter2",
      "min":-100,
      "max":100
    }
  ],
  "address": "149.156.11.4:10053"
}
```

## Configuration keys

* ``method_type``: string, name of SA method to use:
  * ``morris``, when using, set additional keys in config:
    * ``morris_samples_count``: integer
    * ``morris_levels_count``: integer
  * ``sobol``, when using, set additional keys in config:
    * ``sobol_base_inputs_count``: integer
* ``address``: string, address of Scalarm Experiment Manager (without protocol provided)
* credentials - either BasicAuth login/password or X509 proxy certificate used to authenticate in Scalarm
  * BasicAuth
    * ``user``: string, username
    * ``password``: string, password
  * X509 Proxy certificate
    * ``experiment_manager_proxy_path``: string, path to file in PEM format X509 Proxy certificate
* entity to supervise - can be either Scalarm SimulationScenario or Experiment; of course these entities must exists and credentials should allow to view or/and modify it in provided Scalarm instance
  * SimulationScenario - if provided, ID of SimulationScenario will be used to instantiate an Experiment to supervise with sensitivity analysis algorithm
    * ``simulation_id``: string, ID of Scalarm SimulationScenario
  * Experiment - used only if SimulationScenario is not provided (used by Pathfinder)
    * ``experiment_id``: string, ID of Scalarm Experiment to use
* ``parameters``: array, an array of SimulationScenario parameters specifications
  * parameter object
    * ``id``: string
    * ~~``type``: string, currently not used - *all parameters should be float*~~
    * ``min``: float, lower limit of parameter value range to search
    * ``max``: float, upper limit of parameter value range to search
    * ~~``start_value``: float, currently not used~~
* ``fake_experiment`` (optional): boolean, if true - do not use real Scalarm server, randomly generate results instead and write SA results to stdout

# Output

Depending on the configuration, results will be send to Scalarm or only printed on stout (when ``fake_experiment: true``).

## Example outputs

### Method of Sobol

```json
{  
  "sensitivity_analysis_method":"sobol",
  "moes":{  
    "moe_first":{  
      "parameter1":{  
        "sensitivity_indices":0.1553690685569363,
        "total_effect_indices":0.2037052342725223
      },
      "parameter2":{  
        "sensitivity_indices":0.77595737530684716,
        "total_effect_indices":0.85337015856275833
      }
    },
    "moe_random":{  
      "parameter1":{  
        "sensitivity_indices":0.0059681671107936606,
        "total_effect_indices":1.0526041838912277
      },
      "parameter2":{  
        "sensitivity_indices":0.008428390408741087,
        "total_effect_indices":0.9496497682209919
      }
    }
  }
}
```

### Method of Morris



# Technical documentation

Some aspects of internal implementation. Not for regular users :)

## Getting Scalarm Results

After computing simulation by Scalarm:
```
IList<Scalarm.SimulationParams> scalarmResults = experiment.GetResults()
```

This is an array of ``Scalarm.SimulationParams``. For example single ``SimulationParams`` (some JSON-like pseudocode):
```
{
  Input: { // Scalarm.ValuesMap
    "parameter1": 1000.0,
    "paraneter2": 100.0
  },
  Output: { // Scalarm.ValuesMap
    "moe": 100000.0
  }
}
```
