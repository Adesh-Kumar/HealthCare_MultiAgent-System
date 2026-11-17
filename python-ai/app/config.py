from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_env: str = "local"
    openai_api_key: str | None = None
    openai_model: str = "gpt-4o-mini"
    dotnet_api_base_url: str = "http://127.0.0.1:5080"
    dotnet_api_timeout_seconds: float = 20.0
    api_bearer_token: str = "local-dev-token"

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")


settings = Settings()
