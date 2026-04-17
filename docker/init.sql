-- Initial database schema for Parental Control System

CREATE TYPE account_type AS ENUM ('Child', 'Parent', 'Technical');

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(64) NOT NULL UNIQUE,
    full_name VARCHAR(255),
    email VARCHAR(255),
    account_type VARCHAR(20) NOT NULL DEFAULT 'Child',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT username_format CHECK (username ~ '^[a-z][a-z0-9_-]{0,31}$'),
    CONSTRAINT valid_account_type CHECK (account_type IN ('Child', 'Parent', 'Technical'))
);

CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_account_type ON users(account_type) WHERE account_type = 'Child';

CREATE TABLE computers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    hostname VARCHAR(255) NOT NULL UNIQUE,
    machine_id VARCHAR(64) NOT NULL UNIQUE,
    os_info VARCHAR(255),
    api_key VARCHAR(128),
    last_seen_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_computers_hostname ON computers(hostname);
CREATE INDEX idx_computers_active ON computers(is_active) WHERE is_active = true;

CREATE TABLE time_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    monday_limit INT NOT NULL DEFAULT 0 CHECK (monday_limit >= 0),
    tuesday_limit INT NOT NULL DEFAULT 0 CHECK (tuesday_limit >= 0),
    wednesday_limit INT NOT NULL DEFAULT 0 CHECK (wednesday_limit >= 0),
    thursday_limit INT NOT NULL DEFAULT 0 CHECK (thursday_limit >= 0),
    friday_limit INT NOT NULL DEFAULT 0 CHECK (friday_limit >= 0),
    saturday_limit INT NOT NULL DEFAULT 0 CHECK (saturday_limit >= 0),
    sunday_limit INT NOT NULL DEFAULT 0 CHECK (sunday_limit >= 0),
    weekly_limit INT NOT NULL DEFAULT 0 CHECK (weekly_limit >= 0),
    enforcement_action VARCHAR(20) NOT NULL DEFAULT 'logout' CHECK (enforcement_action IN ('logout', 'lock', 'notify')),
    warning_times INT[] NOT NULL DEFAULT ARRAY[15, 10, 5, 1],
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT unique_user_profile_name UNIQUE (user_id, name)
);

CREATE INDEX idx_time_profiles_user ON time_profiles(user_id);
CREATE INDEX idx_time_profiles_active ON time_profiles(is_active) WHERE is_active = true;

CREATE TABLE allowed_hours (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id UUID NOT NULL REFERENCES time_profiles(id) ON DELETE CASCADE,
    day_of_week INT NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    CONSTRAINT valid_time_range CHECK (start_time < end_time),
    CONSTRAINT unique_profile_day_time UNIQUE (profile_id, day_of_week, start_time, end_time)
);

CREATE INDEX idx_allowed_hours_profile ON allowed_hours(profile_id);
CREATE INDEX idx_allowed_hours_day ON allowed_hours(day_of_week);

CREATE TABLE sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    computer_id UUID NOT NULL REFERENCES computers(id) ON DELETE CASCADE,
    session_start TIMESTAMPTZ NOT NULL,
    session_end TIMESTAMPTZ,
    active_minutes INT NOT NULL DEFAULT 0,
    idle_minutes INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    termination_reason VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_sessions_user ON sessions(user_id);
CREATE INDEX idx_sessions_computer ON sessions(computer_id);
CREATE INDEX idx_sessions_active ON sessions(is_active) WHERE is_active = true;
CREATE INDEX idx_sessions_start ON sessions(session_start DESC);

CREATE TABLE time_usage (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    computer_id UUID NOT NULL REFERENCES computers(id) ON DELETE CASCADE,
    session_id UUID REFERENCES sessions(id) ON DELETE SET NULL,
    usage_date DATE NOT NULL,
    minutes_used INT NOT NULL DEFAULT 0,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT unique_user_computer_date UNIQUE (user_id, computer_id, usage_date)
);

CREATE INDEX idx_time_usage_user_date ON time_usage(user_id, usage_date DESC);
CREATE INDEX idx_time_usage_date ON time_usage(usage_date DESC);

CREATE TABLE time_adjustments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    adjustment_date DATE NOT NULL,
    minutes_adjustment INT NOT NULL,
    reason VARCHAR(500),
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_time_adjustments_user ON time_adjustments(user_id);
CREATE INDEX idx_time_adjustments_date ON time_adjustments(adjustment_date DESC);
