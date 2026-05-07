using Pulumi;
using ArtifactRegistry = Pulumi.Gcp.ArtifactRegistry;
using CloudRun = Pulumi.Gcp.CloudRun;
using CloudRunV2 = Pulumi.Gcp.CloudRunV2;
using CloudScheduler = Pulumi.Gcp.CloudScheduler;
using Compute = Pulumi.Gcp.Compute;
using Dns = Pulumi.Gcp.Dns;
using Logging = Pulumi.Gcp.Logging;
using SecretManager = Pulumi.Gcp.SecretManager;
using ServiceAccount = Pulumi.Gcp.ServiceAccount;
using ServiceNetworking = Pulumi.Gcp.ServiceNetworking;
using Sql = Pulumi.Gcp.Sql;

class WebFundamentalDemoStack : Stack
{
    [Output] public Output<string> WebUrl { get; private set; } = null!;
    [Output] public Output<string> ApiUrl { get; private set; } = null!;
    [Output] public Output<string> RegistryUrl { get; private set; } = null!;

    public WebFundamentalDemoStack()
    {
        var config = new Config();
        var gcpConfig = new Config("gcp");

        var project = gcpConfig.Require("project");
        var region = gcpConfig.Get("region") ?? "europe-central2";
        var zone = $"{region}-a";
        var domain = config.Require("domain");       // root domain, e.g. "yourdomain.com"
        var imageTag = config.Get("imageTag") ?? "latest";
        var dbPassword = config.RequireSecret("dbPassword");
        var deployerSaEmail = config.Require("deployerSaEmail");
        var imageBase = $"{region}-docker.pkg.dev/{project}/webfundamentaldemo";

        var apiDomain = $"api.{domain}";
        var webDomain = $"app.{domain}";

        // ── Artifact Registry ────────────────────────────────────────────────

        _ = new ArtifactRegistry.Repository("registry", new()
        {
            RepositoryId = "webfundamentaldemo",
            Format = "DOCKER",
            Location = region,
        });

        // ── VPC Network ──────────────────────────────────────────────────────

        var network = new Compute.Network("network", new()
        {
            Name = "webfundamentaldemo",
            AutoCreateSubnetworks = false,
        });

        var subnet = new Compute.Subnetwork("subnet", new()
        {
            Name = "webfundamentaldemo",
            IpCidrRange = "10.0.0.0/24",
            Region = region,
            Network = network.Id,
        });

        // ── Service Accounts ─────────────────────────────────────────────────

        var apiSa = new ServiceAccount.Account("api-sa", new()
        {
            AccountId = "webfundamentaldemo-api",
            DisplayName = "WebFundamentalDemo API",
        });

        var workerSa = new ServiceAccount.Account("worker-sa", new()
        {
            AccountId = "webfundamentaldemo-worker",
            DisplayName = "WebFundamentalDemo Worker",
        });

        var migrateSa = new ServiceAccount.Account("migrate-sa", new()
        {
            AccountId = "webfundamentaldemo-migrate",
            DisplayName = "WebFundamentalDemo DB Migration",
        });

        var webSa = new ServiceAccount.Account("web-sa", new()
        {
            AccountId = "webfundamentaldemo-web",
            DisplayName = "WebFundamentalDemo Web",
        });

        var schedulerSa = new ServiceAccount.Account("scheduler-sa", new()
        {
            AccountId = "webfundamentaldemo-scheduler",
            DisplayName = "WebFundamentalDemo Cloud Scheduler",
        });

        // ── Secret Manager ───────────────────────────────────────────────────
        //
        // db-connection-string is populated automatically by Pulumi from the
        // Cloud SQL private IP + dbPassword config. All other secrets must be
        // set manually after the first deploy:
        //   gcloud secrets versions add webfundamentaldemo-<name> --data-file=-

        var jwtKeySecret = CreateSecret("jwt-signing-key");
        var anthropicKeySecret = CreateSecret("anthropic-api-key");
        var finnhubKeySecret = CreateSecret("finnhub-api-key");
        var alphaVantageKeySecret = CreateSecret("alpha-vantage-api-key");
        var dbConnStrSecret = CreateSecret("db-connection-string");

        GrantSecretAccess("db-connstr-to-api", dbConnStrSecret, apiSa.Email);
        GrantSecretAccess("db-connstr-to-worker", dbConnStrSecret, workerSa.Email);
        GrantSecretAccess("db-connstr-to-migrate", dbConnStrSecret, migrateSa.Email);
        GrantSecretAccess("jwt-key-to-api", jwtKeySecret, apiSa.Email);
        GrantSecretAccess("anthropic-key-to-worker", anthropicKeySecret, workerSa.Email);
        GrantSecretAccess("finnhub-key-to-worker", finnhubKeySecret, workerSa.Email);
        GrantSecretAccess("alpha-vantage-key-to-worker", alphaVantageKeySecret, workerSa.Email);

        // The deployer SA needs actAs on each workload SA to update Cloud Run
        // services/jobs that run as those accounts.
        _ = new ServiceAccount.IAMMember("deployer-actAs-api", new ServiceAccount.IAMMemberArgs
        {
            ServiceAccountId = apiSa.Name,
            Role = "roles/iam.serviceAccountUser",
            Member = $"serviceAccount:{deployerSaEmail}",
        });
        _ = new ServiceAccount.IAMMember("deployer-actAs-web", new ServiceAccount.IAMMemberArgs
        {
            ServiceAccountId = webSa.Name,
            Role = "roles/iam.serviceAccountUser",
            Member = $"serviceAccount:{deployerSaEmail}",
        });
        _ = new ServiceAccount.IAMMember("deployer-actAs-migrate", new ServiceAccount.IAMMemberArgs
        {
            ServiceAccountId = migrateSa.Name,
            Role = "roles/iam.serviceAccountUser",
            Member = $"serviceAccount:{deployerSaEmail}",
        });
        _ = new ServiceAccount.IAMMember("deployer-actAs-worker", new ServiceAccount.IAMMemberArgs
        {
            ServiceAccountId = workerSa.Name,
            Role = "roles/iam.serviceAccountUser",
            Member = $"serviceAccount:{deployerSaEmail}",
        });

        // ── Cloud SQL (PostgreSQL) ────────────────────────────────────────────
        //
        // Private IP requires VPC peering with Google's managed services network.
        // db-f1-micro: shared vCPU, 0.6 GB RAM — ~$9/month total (instance + 10 GB SSD).

        var sqlPrivateRange = new Compute.GlobalAddress("sql-private-range", new()
        {
            Purpose = "VPC_PEERING",
            AddressType = "INTERNAL",
            PrefixLength = 16,
            Network = network.Id,
        });

        var sqlPeering = new ServiceNetworking.Connection("sql-peering", new()
        {
            Network = network.Id,
            Service = "servicenetworking.googleapis.com",
            ReservedPeeringRanges = new[] { sqlPrivateRange.Name },
        });

        var sqlInstance = new Sql.DatabaseInstance("postgres", new()
        {
            DatabaseVersion = "POSTGRES_16",
            DeletionProtection = false,
            Settings = new Sql.Inputs.DatabaseInstanceSettingsArgs
            {
                Tier = "db-f1-micro",
                AvailabilityType = "ZONAL",
                DiskType = "PD_SSD",
                DiskSize = 10,
                IpConfiguration = new Sql.Inputs.DatabaseInstanceSettingsIpConfigurationArgs
                {
                    Ipv4Enabled = false,
                    PrivateNetwork = network.Id,
                    EnablePrivatePathForGoogleCloudServices = true,
                },
                BackupConfiguration = new Sql.Inputs.DatabaseInstanceSettingsBackupConfigurationArgs
                {
                    Enabled = true,
                    StartTime = "03:00",
                },
            },
        }, new CustomResourceOptions { DependsOn = new[] { sqlPeering } });

        _ = new Sql.Database("db", new()
        {
            Instance = sqlInstance.Name,
            Name = "webfundamentaldemo",
        });

        _ = new Sql.User("postgres-user", new()
        {
            Instance = sqlInstance.Name,
            Name = "postgres",
            Password = dbPassword,
        });

        // Pulumi writes the full connection string into Secret Manager.
        // Api, Worker, and migration job read it from there at runtime.
        var dbConnStr = Output.Tuple(sqlInstance.PrivateIpAddress, dbPassword)
            .Apply(t => $"Host={t.Item1};Port=5432;Database=webfundamentaldemo;Username=postgres;Password={t.Item2}");

        var dbConnStrVersion = new SecretManager.SecretVersion("db-connection-string-version", new()
        {
            Secret = dbConnStrSecret.Id,
            SecretData = dbConnStr,
        });

        // ── Cloud Run: Api ────────────────────────────────────────────────────
        //
        // Note: the refresh token cookie must be set with Domain=.{domain} so it
        // is sent from app.{domain} to api.{domain} (same eTLD+1, SameSite=Strict honoured).

        var apiService = new CloudRunV2.Service("api", new()
        {
            Name = "webfundamentaldemo-api",
            Location = region,
            Ingress = "INGRESS_TRAFFIC_ALL",
            Template = new CloudRunV2.Inputs.ServiceTemplateArgs
            {
                ServiceAccount = apiSa.Email,
                Scaling = new CloudRunV2.Inputs.ServiceTemplateScalingArgs
                {
                    MinInstanceCount = 1,
                    MaxInstanceCount = 3,
                },
                VpcAccess = new CloudRunV2.Inputs.ServiceTemplateVpcAccessArgs
                {
                    NetworkInterfaces = new[]
                    {
                        new CloudRunV2.Inputs.ServiceTemplateVpcAccessNetworkInterfaceArgs
                        {
                            Network = network.Name,
                            Subnetwork = subnet.Name,
                        }
                    },
                    Egress = "PRIVATE_RANGES_ONLY",
                },
                Containers = new[]
                {
                    new CloudRunV2.Inputs.ServiceTemplateContainerArgs
                    {
                        Image = $"{imageBase}/api:{imageTag}",
                        Ports = new[]
                        {
                            new CloudRunV2.Inputs.ServiceTemplateContainerPortArgs
                            {
                                ContainerPort = 8080,
                            }
                        },
                        Envs = new[]
                        {
                            SvcEnv("ASPNETCORE_ENVIRONMENT", "Production"),
                            SvcEnv("Jwt__Issuer", "webfundamentaldemo-api"),
                            SvcEnv("Jwt__Audience", "webfundamentaldemo-web"),
                            SvcEnv("Cors__AllowedOrigin", $"https://{webDomain}"),
                            SvcSecretEnv("ConnectionStrings__Postgres", dbConnStrSecret),
                            SvcSecretEnv("Jwt__SigningKey", jwtKeySecret),
                        },
                        StartupProbe = new CloudRunV2.Inputs.ServiceTemplateContainerStartupProbeArgs
                        {
                            HttpGet = new CloudRunV2.Inputs.ServiceTemplateContainerStartupProbeHttpGetArgs
                            {
                                Path = "/health",
                                Port = 8080,
                            },
                            InitialDelaySeconds = 5,
                            TimeoutSeconds = 5,
                            PeriodSeconds = 10,
                            FailureThreshold = 6,
                        },
                        Resources = SvcResources("1", "512Mi"),
                    }
                },
            },
        }, new CustomResourceOptions { DependsOn = new[] { dbConnStrVersion } });

        _ = new CloudRunV2.ServiceIamMember("api-public", new()
        {
            Project = project,
            Location = region,
            Name = apiService.Name,
            Role = "roles/run.invoker",
            Member = "allUsers",
        });

        // ── Cloud Run: Web ────────────────────────────────────────────────────

        var webService = new CloudRunV2.Service("web", new()
        {
            Name = "webfundamentaldemo-web",
            Location = region,
            Ingress = "INGRESS_TRAFFIC_ALL",
            Template = new CloudRunV2.Inputs.ServiceTemplateArgs
            {
                ServiceAccount = webSa.Email,
                Scaling = new CloudRunV2.Inputs.ServiceTemplateScalingArgs
                {
                    MinInstanceCount = 0,
                    MaxInstanceCount = 3,
                },
                Containers = new[]
                {
                    new CloudRunV2.Inputs.ServiceTemplateContainerArgs
                    {
                        Image = $"{imageBase}/web:{imageTag}",
                        Ports = new[]
                        {
                            new CloudRunV2.Inputs.ServiceTemplateContainerPortArgs
                            {
                                ContainerPort = 80,
                            }
                        },
                        Envs = new[]
                        {
                            SvcEnv("API_BASE_URL", $"https://{apiDomain}"),
                        },
                        Resources = SvcResources("1", "512Mi"),
                    }
                },
            },
        });

        _ = new CloudRunV2.ServiceIamMember("web-public", new()
        {
            Project = project,
            Location = region,
            Name = webService.Name,
            Role = "roles/run.invoker",
            Member = "allUsers",
        });

        // ── Cloud Run Job: Worker ─────────────────────────────────────────────

        var workerJob = new CloudRunV2.Job("worker", new()
        {
            Name = "webfundamentaldemo-worker",
            Location = region,
            Template = new CloudRunV2.Inputs.JobTemplateArgs
            {
                Template = new CloudRunV2.Inputs.JobTemplateTemplateArgs
                {
                    ServiceAccount = workerSa.Email,
                    MaxRetries = 1,
                    Timeout = "3600s",
                    VpcAccess = new CloudRunV2.Inputs.JobTemplateTemplateVpcAccessArgs
                    {
                        NetworkInterfaces = new[]
                        {
                            new CloudRunV2.Inputs.JobTemplateTemplateVpcAccessNetworkInterfaceArgs
                            {
                                Network = network.Name,
                                Subnetwork = subnet.Name,
                            }
                        },
                        Egress = "PRIVATE_RANGES_ONLY",
                    },
                    Containers = new[]
                    {
                        new CloudRunV2.Inputs.JobTemplateTemplateContainerArgs
                        {
                            Image = $"{imageBase}/worker:{imageTag}",
                            Envs = new[]
                            {
                                JobSecretEnv("ConnectionStrings__Postgres", dbConnStrSecret),
                                JobSecretEnv("Anthropic__ApiKey", anthropicKeySecret),
                                JobSecretEnv("Finnhub__ApiKey", finnhubKeySecret),
                                JobSecretEnv("AlphaVantage__ApiKey", alphaVantageKeySecret),
                            },
                            Resources = new CloudRunV2.Inputs.JobTemplateTemplateContainerResourcesArgs
                            {
                                Limits = new InputMap<string>
                                {
                                    { "cpu", "1" },
                                    { "memory", "512Mi" },
                                },
                            },
                        }
                    },
                },
            },
        }, new CustomResourceOptions { DependsOn = new[] { dbConnStrVersion } });

        // ── Cloud Run Job: DB Migrations ──────────────────────────────────────
        //
        // Triggered by the deploy workflow before updating the Api revision.
        // MaxRetries=0 — failed migrations must not be retried automatically.

        _ = new CloudRunV2.Job("migrate", new()
        {
            Name = "webfundamentaldemo-migrate",
            Location = region,
            Template = new CloudRunV2.Inputs.JobTemplateArgs
            {
                Template = new CloudRunV2.Inputs.JobTemplateTemplateArgs
                {
                    ServiceAccount = migrateSa.Email,
                    MaxRetries = 0,
                    Timeout = "600s",
                    VpcAccess = new CloudRunV2.Inputs.JobTemplateTemplateVpcAccessArgs
                    {
                        NetworkInterfaces = new[]
                        {
                            new CloudRunV2.Inputs.JobTemplateTemplateVpcAccessNetworkInterfaceArgs
                            {
                                Network = network.Name,
                                Subnetwork = subnet.Name,
                            }
                        },
                        Egress = "PRIVATE_RANGES_ONLY",
                    },
                    Containers = new[]
                    {
                        new CloudRunV2.Inputs.JobTemplateTemplateContainerArgs
                        {
                            Image = $"{imageBase}/migrate:{imageTag}",
                            Envs = new[]
                            {
                                JobSecretEnv("ConnectionStrings__Postgres", dbConnStrSecret),
                            },
                            Resources = new CloudRunV2.Inputs.JobTemplateTemplateContainerResourcesArgs
                            {
                                Limits = new InputMap<string>
                                {
                                    { "cpu", "1" },
                                    { "memory", "512Mi" },
                                },
                            },
                        }
                    },
                },
            },
        }, new CustomResourceOptions { DependsOn = new[] { dbConnStrVersion } });

        // ── Cloud Scheduler: nightly Worker trigger ───────────────────────────

        _ = new CloudRunV2.JobIamMember("scheduler-invokes-worker", new()
        {
            Project = project,
            Location = region,
            Name = workerJob.Name,
            Role = "roles/run.invoker",
            Member = schedulerSa.Email.Apply(e => $"serviceAccount:{e}"),
        });

        _ = new CloudScheduler.Job("nightly-worker", new()
        {
            Region = region,
            Schedule = "0 2 * * *",
            TimeZone = "UTC",
            HttpTarget = new CloudScheduler.Inputs.JobHttpTargetArgs
            {
                HttpMethod = "POST",
                Uri = Output.Format($"https://run.googleapis.com/v2/projects/{project}/locations/{region}/jobs/{workerJob.Name}:run"),
                OauthToken = new CloudScheduler.Inputs.JobHttpTargetOauthTokenArgs
                {
                    ServiceAccountEmail = schedulerSa.Email,
                    Scope = "https://www.googleapis.com/auth/cloud-platform",
                },
            },
        });

        // ── Cloud Run Domain Mappings ─────────────────────────────────────────
        //
        // Provides free Google-managed HTTPS certificates per service — no load
        // balancer needed. Prerequisite: verify domain ownership in Google Search
        // Console before running 'pulumi up', otherwise mapping creation fails.

        _ = new CloudRun.DomainMapping("api-domain", new CloudRun.DomainMappingArgs
        {
            Location = region,
            Name = apiDomain,
            Metadata = new CloudRun.Inputs.DomainMappingMetadataArgs
            {
                Namespace = project,
            },
            Spec = new CloudRun.Inputs.DomainMappingSpecArgs
            {
                RouteName = apiService.Name,
            },
        });

        _ = new CloudRun.DomainMapping("web-domain", new CloudRun.DomainMappingArgs
        {
            Location = region,
            Name = webDomain,
            Metadata = new CloudRun.Inputs.DomainMappingMetadataArgs
            {
                Namespace = project,
            },
            Spec = new CloudRun.Inputs.DomainMappingSpecArgs
            {
                RouteName = webService.Name,
            },
        });

        // ── Cloud DNS ─────────────────────────────────────────────────────────

        var dnsZone = new Dns.ManagedZone("dns-zone", new()
        {
            DnsName = $"{domain}.",
            Visibility = "public",
        });

        // Cloud Run subdomain mappings use a stable CNAME target.
        _ = new Dns.RecordSet("api-cname", new()
        {
            Name = $"{apiDomain}.",
            Type = "CNAME",
            Ttl = 300,
            ManagedZone = dnsZone.Name,
            Rrdatas = new[] { "ghs.googlehosted.com." },
        });

        _ = new Dns.RecordSet("web-cname", new()
        {
            Name = $"{webDomain}.",
            Type = "CNAME",
            Ttl = 300,
            ManagedZone = dnsZone.Name,
            Rrdatas = new[] { "ghs.googlehosted.com." },
        });

        // ── Cloud Logging ─────────────────────────────────────────────────────

        _ = new Logging.ProjectBucketConfig("logs", new()
        {
            Project = project,
            Location = "global",
            BucketId = "webfundamentaldemo",
            RetentionDays = 14,
        });

        // ── Outputs ───────────────────────────────────────────────────────────

        WebUrl = Output.Create($"https://{webDomain}");
        ApiUrl = Output.Create($"https://{apiDomain}");
        RegistryUrl = Output.Create(imageBase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SecretManager.Secret CreateSecret(string name) =>
        new($"secret-{name}", new SecretManager.SecretArgs
        {
            SecretId = $"webfundamentaldemo-{name}",
            Replication = new SecretManager.Inputs.SecretReplicationArgs
            {
                Auto = new SecretManager.Inputs.SecretReplicationAutoArgs(),
            },
        });

    private static void GrantSecretAccess(
        string resourceName,
        SecretManager.Secret secret,
        Output<string> saEmail) =>
        new SecretManager.SecretIamMember(resourceName, new SecretManager.SecretIamMemberArgs
        {
            SecretId = secret.SecretId,
            Role = "roles/secretmanager.secretAccessor",
            Member = saEmail.Apply(email => $"serviceAccount:{email}"),
        });

    private static CloudRunV2.Inputs.ServiceTemplateContainerEnvArgs SvcEnv(
        string name, string value) =>
        new() { Name = name, Value = value };

    private static CloudRunV2.Inputs.ServiceTemplateContainerEnvArgs SvcSecretEnv(
        string name, SecretManager.Secret secret) =>
        new()
        {
            Name = name,
            ValueSource = new CloudRunV2.Inputs.ServiceTemplateContainerEnvValueSourceArgs
            {
                SecretKeyRef = new CloudRunV2.Inputs.ServiceTemplateContainerEnvValueSourceSecretKeyRefArgs
                {
                    Secret = secret.SecretId,
                    Version = "latest",
                }
            }
        };

    private static CloudRunV2.Inputs.JobTemplateTemplateContainerEnvArgs JobSecretEnv(
        string name, SecretManager.Secret secret) =>
        new()
        {
            Name = name,
            ValueSource = new CloudRunV2.Inputs.JobTemplateTemplateContainerEnvValueSourceArgs
            {
                SecretKeyRef = new CloudRunV2.Inputs.JobTemplateTemplateContainerEnvValueSourceSecretKeyRefArgs
                {
                    Secret = secret.SecretId,
                    Version = "latest",
                }
            }
        };

    private static CloudRunV2.Inputs.ServiceTemplateContainerResourcesArgs SvcResources(
        string cpu, string memory) =>
        new()
        {
            Limits = new InputMap<string>
            {
                { "cpu", cpu },
                { "memory", memory },
            }
        };
}
