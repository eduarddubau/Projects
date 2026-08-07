# Included explicitly by every location in default.conf.
#
# This exists because `proxy_set_header` inheritance in nginx is all-or-nothing:
# a location that defines even one of them inherits none from the server block.
# Setting these once at server level looks like it works and silently stops the
# moment a location adds its own header — Host falls back to $proxy_host (the
# upstream's name) and the X-Forwarded-* set disappears.
#
# The .inc extension keeps it out of the conf.d/*.conf auto-include, so it only
# ever takes effect where it is asked for.

proxy_set_header Host              $host;
proxy_set_header X-Real-IP         $remote_addr;
proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-Host  $host;
