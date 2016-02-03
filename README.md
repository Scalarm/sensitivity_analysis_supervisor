This is a Scalarm Pathfinder supervisor. For more information see: https://github.com/Scalarm/scalarm_experiment_supervisor

# Execution parameters

* ``mono Program.exe`` will read config from ``config.json`` in current directory
* ``mono Program.exe -config <config_path>`` will read config from ``<config_path>`` file
* ``mono Program.exe -stdin`` will read config from standard input (e.g. ``cat config.json | Program.exe`` on *NIX)


# Configuration

## Example configuration

```json
{
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

## Configuration keys

* ``morris_samples_count``: integer >= 1, samples count parameter for Morris algorithm
* ``morris_levels_count``: integer >= 1, levels count parameter for Morris algorithm
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
